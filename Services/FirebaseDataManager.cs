using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fix_It.Models;

namespace Fix_It.Services
{
    // Talks to Firestore and Firebase Storage over their REST APIs — same lightweight
    // approach as FirebaseAuthManager, no service account or Firestore SDK needed.
    public static class FirebaseDataManager
    {
        const string ProjectId = "fix-it-765d8";

        // Check against Firebase console -> Storage if uploads start failing with a 404 —
        // older projects use "{ProjectId}.appspot.com" instead.
        const string StorageBucket = "fix-it-765d8.firebasestorage.app";

        const string FirestoreBaseUrl = $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents";
        const string StorageBaseUrl = $"https://firebasestorage.googleapis.com/v0/b/{StorageBucket}/o";
        const string IssueReportsCollection = "issueReports";
        const string NotificationsCollection = "notifications";

        static readonly HttpClient _httpClient = new();

        // Uploads the photo first (if any) so its URL can be stamped onto the report before saving.
        public static async Task<bool> SaveIssueReportAsync(IssueReport report, byte[]? photoBytes)
        {
            try
            {
                var idToken = await FirebaseAuthManager.GetIdTokenAsync();
                if (idToken is null)
                    return false;

                if (photoBytes is not null)
                {
                    var photoUrl = await UploadPhotoAsync(photoBytes, report.CreatedByFirebaseUid, idToken);
                    if (photoUrl is null)
                        return false;

                    report.PhotoUrl = photoUrl;
                }

                report.Status = "Open";
                report.Activity = new ObservableCollection<IssueActivityEntry>
                {
                    new() { ActorEmail = report.CreatedByEmail, Action = "reported the issue", TimestampUtc = report.CreatedAtUtc }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{FirestoreBaseUrl}/{IssueReportsCollection}")
                {
                    Content = JsonContent.Create(new { fields = BuildFields(report) })
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveIssueReportAsync failed: {ex.Message}");
                return false;
            }
        }

        // Lists every report, not just the current user's — anyone can view and resolve any issue.
        public static async Task<List<IssueReport>> GetAllIssueReportsAsync()
        {
            try
            {
                var idToken = await FirebaseAuthManager.GetIdTokenAsync();
                if (idToken is null)
                    return new List<IssueReport>();

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{FirestoreBaseUrl}/{IssueReportsCollection}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return new List<IssueReport>();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var json = await JsonDocument.ParseAsync(stream);

                var reports = new List<IssueReport>();
                // An empty collection has no "documents" property at all, not an empty array.
                if (json.RootElement.TryGetProperty("documents", out var documents))
                {
                    foreach (var document in documents.EnumerateArray())
                        reports.Add(ParseIssueReport(document));
                }

                return reports.OrderByDescending(r => r.CreatedAtUtc).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetAllIssueReportsAsync failed: {ex.Message}");
                return new List<IssueReport>();
            }
        }

        // Used by IssueDetailPage to refresh after returning from an edit.
        public static async Task<IssueReport?> GetIssueReportByIdAsync(string id)
        {
            try
            {
                var idToken = await FirebaseAuthManager.GetIdTokenAsync();
                if (idToken is null)
                    return null;

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{FirestoreBaseUrl}/{IssueReportsCollection}/{id}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return null;

                using var stream = await response.Content.ReadAsStreamAsync();
                using var json = await JsonDocument.ParseAsync(stream);
                return ParseIssueReport(json.RootElement);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetIssueReportByIdAsync failed: {ex.Message}");
                return null;
            }
        }

        // Ownership isn't checked here — the caller (IssueDetailViewModel) only shows Edit
        // to the report's creator, so only they ever reach this path.
        public static async Task<bool> UpdateIssueReportAsync(IssueReport report, byte[]? newPhotoBytes, string actorEmail)
        {
            try
            {
                var idToken = await FirebaseAuthManager.GetIdTokenAsync();
                if (idToken is null)
                    return false;

                if (newPhotoBytes is not null)
                {
                    var photoUrl = await UploadPhotoAsync(newPhotoBytes, report.CreatedByFirebaseUid, idToken);
                    if (photoUrl is null)
                        return false;

                    report.PhotoUrl = photoUrl;
                }

                // Insert at the front so the newest entry shows first.
                report.Activity.Insert(0, new IssueActivityEntry
                {
                    ActorEmail = actorEmail,
                    Action = "updated the report",
                    TimestampUtc = DateTime.UtcNow
                });

                var fieldPaths = new[] { "title", "location", "description", "priority", "photoUrl", "activity" };
                return await PatchAsync(report.Id, BuildFields(report), fieldPaths, idToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateIssueReportAsync failed: {ex.Message}");
                return false;
            }
        }

        // Any signed-in user can resolve any issue — no ownership check by design.
        public static async Task<bool> ResolveIssueReportAsync(IssueReport report, string actorEmail)
        {
            try
            {
                var idToken = await FirebaseAuthManager.GetIdTokenAsync();
                if (idToken is null)
                    return false;

                report.Status = "Resolved";
                report.Activity.Insert(0, new IssueActivityEntry
                {
                    ActorEmail = actorEmail,
                    Action = "marked this issue resolved",
                    TimestampUtc = DateTime.UtcNow
                });

                var fieldPaths = new[] { "status", "activity" };
                return await PatchAsync(report.Id, BuildFields(report), fieldPaths, idToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ResolveIssueReportAsync failed: {ex.Message}");
                return false;
            }
        }

        // Persists the notification so the Notifications tab keeps history after the
        // OS tray notification is dismissed.
        public static async Task<bool> LogNotificationAsync(string title, string message)
        {
            try
            {
                var idToken = await FirebaseAuthManager.GetIdTokenAsync();
                if (idToken is null)
                    return false;

                var fields = new
                {
                    title = new { stringValue = title },
                    message = new { stringValue = message },
                    timestampUtc = new { timestampValue = DateTime.UtcNow.ToString("o") }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{FirestoreBaseUrl}/{NotificationsCollection}")
                {
                    Content = JsonContent.Create(new { fields })
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LogNotificationAsync failed: {ex.Message}");
                return false;
            }
        }

        // Shared history across all users — no per-user read/unread state.
        public static async Task<List<NotificationLogEntry>> GetRecentNotificationsAsync(int limit = 30)
        {
            try
            {
                var idToken = await FirebaseAuthManager.GetIdTokenAsync();
                if (idToken is null)
                    return new List<NotificationLogEntry>();

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{FirestoreBaseUrl}/{NotificationsCollection}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return new List<NotificationLogEntry>();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var json = await JsonDocument.ParseAsync(stream);

                var entries = new List<NotificationLogEntry>();
                if (json.RootElement.TryGetProperty("documents", out var documents))
                {
                    foreach (var document in documents.EnumerateArray())
                        entries.Add(ParseNotificationLogEntry(document));
                }

                return entries.OrderByDescending(n => n.TimestampUtc).Take(limit).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetRecentNotificationsAsync failed: {ex.Message}");
                return new List<NotificationLogEntry>();
            }
        }

        static NotificationLogEntry ParseNotificationLogEntry(JsonElement document)
        {
            var fields = document.GetProperty("fields");

            return new NotificationLogEntry
            {
                Title = GetString(fields, "title"),
                Message = GetString(fields, "message"),
                TimestampUtc = DateTime.Parse(
                    fields.GetProperty("timestampUtc").GetProperty("timestampValue").GetString()!,
                    null,
                    DateTimeStyles.RoundtripKind)
            };
        }

        static async Task<bool> PatchAsync(string documentId, Dictionary<string, object> allFields, string[] fieldPaths, string idToken)
        {
            // updateMask needs a value for every listed field, or it gets deleted instead of
            // left alone — so only send the fields named in fieldPaths.
            var fieldsToSend = fieldPaths.ToDictionary(path => path, path => allFields[path]);
            var maskQuery = string.Join("&", fieldPaths.Select(p => $"updateMask.fieldPaths={Uri.EscapeDataString(p)}"));

            using var request = new HttpRequestMessage(HttpMethod.Patch, $"{FirestoreBaseUrl}/{IssueReportsCollection}/{documentId}?{maskQuery}")
            {
                Content = JsonContent.Create(new { fields = fieldsToSend })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        static async Task<string?> UploadPhotoAsync(byte[] photoBytes, string firebaseUid, string idToken)
        {
            // The "/" in the path needs to be percent-encoded for Storage's REST API.
            var objectPath = $"issuePhotos/{firebaseUid}/{Guid.NewGuid()}.jpg";
            var encodedPath = Uri.EscapeDataString(objectPath);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{StorageBaseUrl}?name={encodedPath}")
            {
                Content = new ByteArrayContent(photoBytes)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);
            var downloadToken = json.RootElement.GetProperty("downloadTokens").GetString();

            return $"https://firebasestorage.googleapis.com/v0/b/{StorageBucket}/o/{encodedPath}?alt=media&token={downloadToken}";
        }

        // Full field set for a create; PatchAsync filters this down for partial updates.
        static Dictionary<string, object> BuildFields(IssueReport report) => new()
        {
            ["title"] = StringValue(report.Title),
            ["location"] = StringValue(report.Location),
            ["description"] = StringValue(report.Description),
            ["priority"] = StringValue(report.Priority),
            ["photoUrl"] = StringValue(report.PhotoUrl ?? string.Empty),
            ["status"] = StringValue(report.Status),
            ["createdByFirebaseUid"] = StringValue(report.CreatedByFirebaseUid),
            ["createdByEmail"] = StringValue(report.CreatedByEmail),
            ["createdAtUtc"] = new { timestampValue = report.CreatedAtUtc.ToString("o") },
            ["activity"] = ActivityArrayValue(report.Activity)
        };

        // Shared by every place a Firestore document needs turning into an IssueReport.
        static IssueReport ParseIssueReport(JsonElement document)
        {
            var fields = document.GetProperty("fields");

            return new IssueReport
            {
                // "name" is the full resource path — the id is just the last segment.
                Id = document.GetProperty("name").GetString()!.Split('/').Last(),
                Title = GetString(fields, "title"),
                Location = GetString(fields, "location"),
                Description = GetString(fields, "description"),
                Priority = GetString(fields, "priority"),
                PhotoUrl = GetString(fields, "photoUrl") is { Length: > 0 } url ? url : null,
                Status = GetString(fields, "status") is { Length: > 0 } status ? status : "Open",
                CreatedByFirebaseUid = GetString(fields, "createdByFirebaseUid"),
                CreatedByEmail = GetString(fields, "createdByEmail"),
                CreatedAtUtc = DateTime.Parse(
                    fields.GetProperty("createdAtUtc").GetProperty("timestampValue").GetString()!,
                    null,
                    DateTimeStyles.RoundtripKind),
                Activity = ParseActivity(fields)
            };
        }

        // Firestore's array-of-maps shape — easy to get wrong, kept in one place with ParseActivity.
        static object ActivityArrayValue(IEnumerable<IssueActivityEntry> activity) => new
        {
            arrayValue = new
            {
                values = activity.Select(entry => new
                {
                    mapValue = new
                    {
                        fields = new
                        {
                            actorEmail = new { stringValue = entry.ActorEmail },
                            action = new { stringValue = entry.Action },
                            timestampUtc = new { timestampValue = entry.TimestampUtc.ToString("o") }
                        }
                    }
                })
            }
        };

        static ObservableCollection<IssueActivityEntry> ParseActivity(JsonElement fields)
        {
            var entries = new List<IssueActivityEntry>();

            if (!fields.TryGetProperty("activity", out var activityField) ||
                !activityField.TryGetProperty("arrayValue", out var arrayValue) ||
                !arrayValue.TryGetProperty("values", out var values))
                return new ObservableCollection<IssueActivityEntry>();

            foreach (var value in values.EnumerateArray())
            {
                var entryFields = value.GetProperty("mapValue").GetProperty("fields");
                entries.Add(new IssueActivityEntry
                {
                    ActorEmail = GetString(entryFields, "actorEmail"),
                    Action = GetString(entryFields, "action"),
                    TimestampUtc = DateTime.Parse(
                        entryFields.GetProperty("timestampUtc").GetProperty("timestampValue").GetString()!,
                        null,
                        DateTimeStyles.RoundtripKind)
                });
            }

            // Sort explicitly rather than trust stored order, so old data from before entries
            // were inserted newest-first still displays correctly.
            return new ObservableCollection<IssueActivityEntry>(entries.OrderByDescending(e => e.TimestampUtc));
        }

        static object StringValue(string value) => new { stringValue = value };

        static string GetString(JsonElement fields, string key) =>
            fields.TryGetProperty(key, out var field) && field.TryGetProperty("stringValue", out var value)
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }
}
