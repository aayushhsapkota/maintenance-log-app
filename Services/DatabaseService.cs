using Fix_It.Models;
using SQLite;

namespace Fix_It.Services
{
    public class DatabaseService
    {
        readonly SQLiteAsyncConnection _connection;
        readonly SemaphoreSlim _initLock = new(1, 1);
        bool _isInitialized;

        public DatabaseService()
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "fixit.db3");
            _connection = new SQLiteAsyncConnection(dbPath);
        }

        // This method makes sure the database tables exist, but only sets them up ONE time,
        // even if multiple parts of the app try to call it at the same time.
        async Task EnsureInitializedAsync()
        {
            if (_isInitialized)
                return;

            // Wait here until we can safely get exclusive access ("the key").
            // If another call is already inside this method setting things up,
            // this line will pause and wait its turn instead of running at the same time.
            await _initLock.WaitAsync();
            try
            {
                // It's possible another call finished the setup WHILE we were waiting above.
                // If so, there's no need to redo the work — just exit.
                if (_isInitialized)
                    return;

                await _connection.CreateTableAsync<User>();
                await _connection.CreateTableAsync<IssueReport>();
                _isInitialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            await EnsureInitializedAsync();
            return await _connection.Table<User>()
                .Where(u => u.Username == username)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> RegisterUserAsync(string username, string password)
        {
            await EnsureInitializedAsync();

            var existing = await GetUserByUsernameAsync(username);
            if (existing is not null)
                return false;

            var (hash, salt) = PasswordHasher.HashPassword(password);
            var user = new User
            {
                Username = username,
                PasswordHash = hash,
                PasswordSalt = salt
            };

            await _connection.InsertAsync(user);
            return true;
        }

        public async Task<User?> ValidateUserAsync(string username, string password)
        {
            var user = await GetUserByUsernameAsync(username);
            if (user is null)
                return null;

            return PasswordHasher.VerifyPassword(password, user.PasswordHash, user.PasswordSalt)
                ? user
                : null;
        }

        public async Task SaveIssueReportAsync(IssueReport report)
        {
            await EnsureInitializedAsync();
            await _connection.InsertAsync(report);
        }

        public async Task<List<IssueReport>> GetIssueReportsByUserAsync(int userId)
        {
            await EnsureInitializedAsync();
            return await _connection.Table<IssueReport>()
                .Where(r => r.CreatedByUserId == userId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToListAsync();
        }
    }
}
