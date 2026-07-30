using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Fix_It.Models;

namespace Fix_It.Services
{
    // Talks to Firestore and Firebase Storage directly over their REST APIs, the same
    // lightweight approach FirebaseAuthManager uses for Auth — no service account, no
    // Google.Cloud.Firestore SDK, just the project's public config values plus the signed-in
    // user's ID token for authenticated requests.
    public static class FirebaseDataManager
    {
        const string ProjectId = "fix-it-765d8";

        // IMPORTANT: verify this against Firebase console -> Storage -> the "gs://..." bucket
        // name shown at the top. Projects created before Oct 2024 usually use
        // "{ProjectId}.appspot.com" instead of "{ProjectId}.firebasestorage.app" — update this
        // constant if uploads fail with a 404.
        const string StorageBucket = "fix-it-765d8.firebasestorage.app";

        const string FirestoreBaseUrl = $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents";
        const string StorageBaseUrl = $"https://firebasestorage.googleapis.com/v0/b/{StorageBucket}/o";
        const string IssueReportsCollection = "issueReports";

        static readonly HttpClient _httpClient = new();

        // Saves an issue report to Firestore, uploading photoBytes to Firebase Storage first
        // (if provided) and stamping the resulting download URL onto report.PhotoUrl before
        // writing the document — mirrors how RegisterAccount mutates the User it's given.
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

                var document = new
                {
                    fields = new Dictionary<string, object>
                    {
                        ["title"] = StringValue(report.Title),
                        ["location"] = StringValue(report.Location),
                        ["description"] = StringValue(report.Description),
                        ["priority"] = StringValue(report.Priority),
                        ["photoUrl"] = StringValue(report.PhotoUrl ?? string.Empty),
                        ["createdByFirebaseUid"] = StringValue(report.CreatedByFirebaseUid),
                        ["createdAtUtc"] = new { timestampValue = report.CreatedAtUtc.ToString("o") }
                    }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{FirestoreBaseUrl}/{IssueReportsCollection}")
                {
                    Content = JsonContent.Create(document)
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

        // Firestore's REST API only supports filtering via the :runQuery structured-query
        // endpoint (a plain GET-with-query-string isn't a thing here). We filter server-side by
        // user and sort client-side afterward, rather than adding an orderBy to the query too —
        // that would need a composite index created in the Firestore console before it'd work,
        // which is an easy thing to get tripped up by for a dataset this small.
        public static async Task<List<IssueReport>> GetIssueReportsByUserAsync(string firebaseUid)
        {
            try
            {
                var idToken = await FirebaseAuthManager.GetIdTokenAsync();
                if (idToken is null)
                    return new List<IssueReport>();

                var query = new
                {
                    structuredQuery = new
                    {
                        from = new[] { new { collectionId = IssueReportsCollection } },
                        where = new
                        {
                            fieldFilter = new
                            {
                                field = new { fieldPath = "createdByFirebaseUid" },
                                op = "EQUAL",
                                value = new { stringValue = firebaseUid }
                            }
                        }
                    }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{FirestoreBaseUrl}:runQuery")
                {
                    Content = JsonContent.Create(query)
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return new List<IssueReport>();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var json = await JsonDocument.ParseAsync(stream);

                var reports = new List<IssueReport>();
                foreach (var entry in json.RootElement.EnumerateArray())
                {
                    // Entries with no match (progress-only messages) have no "document" field.
                    if (!entry.TryGetProperty("document", out var document))
                        continue;

                    var fields = document.GetProperty("fields");
                    reports.Add(new IssueReport
                    {
                        Title = GetString(fields, "title"),
                        Location = GetString(fields, "location"),
                        Description = GetString(fields, "description"),
                        Priority = GetString(fields, "priority"),
                        PhotoUrl = GetString(fields, "photoUrl") is { Length: > 0 } url ? url : null,
                        CreatedByFirebaseUid = GetString(fields, "createdByFirebaseUid"),
                        CreatedAtUtc = DateTime.Parse(
                            fields.GetProperty("createdAtUtc").GetProperty("timestampValue").GetString()!,
                            null,
                            System.Globalization.DateTimeStyles.RoundtripKind)
                    });
                }

                return reports.OrderByDescending(r => r.CreatedAtUtc).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetIssueReportsByUserAsync failed: {ex.Message}");
                return new List<IssueReport>();
            }
        }

        static async Task<string?> UploadPhotoAsync(byte[] photoBytes, string firebaseUid, string idToken)
        {
            // Firebase Storage's REST API treats the whole object path as one opaque segment —
            // the "/" separators need to be percent-encoded, not left as literal slashes.
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

        static object StringValue(string value) => new { stringValue = value };

        static string GetString(JsonElement fields, string key) =>
            fields.TryGetProperty(key, out var field) && field.TryGetProperty("stringValue", out var value)
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }
}
