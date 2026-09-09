using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DigiPasal.Models;
using SQLite;

namespace DigiPasal.Services;

public class AuthService
{
    private static readonly Lazy<AuthService> _lazyInstance = new(() => new AuthService());

    public static AuthService Instance => _lazyInstance.Value;

    private const int Pbkdf2Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MinPasswordLength = 8;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(30);
    private const string SessionKey = "auth_username";

    private readonly DatabaseService _dbService;
    private int _failedAttempts;
    private DateTime _lockoutUntil;
    private User? _currentUser;

    public bool IsAuthenticated { get; private set; }

    public bool IsLockedOut => DateTime.UtcNow < _lockoutUntil;

    public TimeSpan LockoutRemaining => IsLockedOut ? _lockoutUntil - DateTime.UtcNow : TimeSpan.Zero;

    public User? CurrentUser => _currentUser;

    public string? CurrentUsername => _currentUser?.Username;

    private AuthService()
    {
        _dbService = DatabaseService.Instance;
    }

    private SQLiteAsyncConnection Database => _dbService.Database;

    private static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Pbkdf2Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        if (string.IsNullOrEmpty(stored))
            return false;

        if (stored.IndexOf('.') < 0)
            return VerifyLegacySha256(password, stored);

        var parts = stored.Split('.');
        if (parts.Length != 3)
            return false;

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iterations) ||
            iterations < 1)
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] candidate = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(expected, candidate);
    }

    private static bool VerifyLegacySha256(string password, string stored)
    {
        byte[] storedBytes;
        try
        {
            storedBytes = Convert.FromBase64String(stored);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] candidate = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return CryptographicOperations.FixedTimeEquals(storedBytes, candidate);
    }

    private static bool RequiresPasswordUpgrade(string stored)
    {
        return !string.IsNullOrEmpty(stored) && stored.IndexOf('.') < 0;
    }

    public async Task<bool> HasAnyUserAsync()
    {
        var count = await Database.Table<User>().CountAsync();
        return count > 0;
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        username = username?.Trim() ?? string.Empty;
        return await Database.Table<User>()
            .Where(u => u.Username.ToLowerInvariant() == username.ToLowerInvariant())
            .FirstOrDefaultAsync();
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        if (_currentUser != null)
            return _currentUser;

        return await Database.Table<User>().FirstOrDefaultAsync();
    }

    public async Task RegisterUserAsync(string username, string password, string fullName, string shopName)
    {
        username = username?.Trim() ?? string.Empty;
        fullName = fullName?.Trim() ?? string.Empty;
        shopName = shopName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
            throw new ArgumentException($"Password must be at least {MinPasswordLength} characters.");
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.");
        if (string.IsNullOrWhiteSpace(shopName))
            throw new ArgumentException("Shop name is required.");

        var existing = await GetUserByUsernameAsync(username);
        if (existing != null)
            throw new InvalidOperationException("A user with this username already exists.");

        if (!await HasDeviceLockScreenAsync())
            throw new InvalidOperationException("Set up a device lock screen (PIN/Pattern/Password) first for security.");

        var user = new User
        {
            Username = username,
            PasswordHash = HashPassword(password),
            FullName = fullName,
            ShopName = shopName,
            IsMaster = true,
            CreatedAt = DateTime.UtcNow
        };

        await Database.InsertAsync(user);
    }

    public async Task<User?> LoginAsync(string username, string password)
    {
        if (IsLockedOut)
            return null;

        var user = await GetUserByUsernameAsync(username);
        if (user == null || !VerifyPassword(password, user.PasswordHash))
        {
            RegisterFailedAttempt();
            return null;
        }

        _failedAttempts = 0;

        if (RequiresPasswordUpgrade(user.PasswordHash))
        {
            user.PasswordHash = HashPassword(password);
            await Database.UpdateAsync(user);
        }

        user.LastLogin = DateTime.UtcNow;
        await Database.UpdateAsync(user);

        await SetAuthenticatedAsync(user);
        return user;
    }

    private void RegisterFailedAttempt()
    {
        _failedAttempts++;
        if (_failedAttempts >= MaxFailedAttempts)
        {
            _lockoutUntil = DateTime.UtcNow + LockoutDuration;
            _failedAttempts = 0;
        }
    }

    public void ResetLockout()
    {
        _failedAttempts = 0;
        _lockoutUntil = DateTime.MinValue;
    }

    public async Task RestoreSessionAsync()
    {

        var savedUsername = Preferences.Default.Get(SessionKey, string.Empty);
        if (string.IsNullOrEmpty(savedUsername))
            return;

        var user = await GetUserByUsernameAsync(savedUsername);
        if (user != null)
        {
            _currentUser = user;
            IsAuthenticated = true;
        }
        else
        {
            Preferences.Default.Remove(SessionKey);
        }
    }

    private async Task SetAuthenticatedAsync(User user)
    {
        _currentUser = user;
        IsAuthenticated = true;
        Preferences.Default.Set(SessionKey, user.Username);
        await Task.CompletedTask;
    }

    private async Task SetAuthenticatedFromCurrentUserAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user != null)
            await SetAuthenticatedAsync(user);
    }

    public async Task<bool> ResetPasswordAsync(string username, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinPasswordLength)
            return false;


        var user = await GetUserByUsernameAsync(username);
        if (user == null)
            return false;

        user.PasswordHash = HashPassword(newPassword);
        await Database.UpdateAsync(user);
        return true;
    }

    public async Task<bool> ChangePasswordAsync(string username, string oldPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinPasswordLength)
            return false;


        var user = await GetUserByUsernameAsync(username);
        if (user == null)
            return false;

        if (!VerifyPassword(oldPassword, user.PasswordHash))
            return false;

        user.PasswordHash = HashPassword(newPassword);
        await Database.UpdateAsync(user);
        return true;
    }

    public async Task<bool> UpdateProfileAsync(string username, string fullName, string shopName)
    {
        fullName = fullName?.Trim() ?? string.Empty;
        shopName = shopName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.");
        if (string.IsNullOrWhiteSpace(shopName))
            throw new ArgumentException("Shop name is required.");


        var user = await GetUserByUsernameAsync(username);
        if (user == null)
            return false;

        user.FullName = fullName;
        user.ShopName = shopName;
        await Database.UpdateAsync(user);

        if (_currentUser?.Id == user.Id)
            _currentUser = user;

        return true;
    }

    public async Task<bool> DeleteAccountAsync(string username, string password)
    {

        var user = await GetUserByUsernameAsync(username);
        if (user == null)
            return false;

        if (!VerifyPassword(password, user.PasswordHash))
            return false;

        await Database.DeleteAsync(user);
        IsAuthenticated = false;
        _currentUser = null;
        Preferences.Default.Remove(SessionKey);
        ResetLockout();
        return true;
    }

    public void SignOut()
    {
        IsAuthenticated = false;
        _currentUser = null;
        Preferences.Default.Remove(SessionKey);
        ResetLockout();
    }

    public async Task<bool> HasDeviceLockScreenAsync()
    {
        return await Task.Run(() =>
        {
#if ANDROID
            try
            {
                var activity = MainActivity.Instance;
                return activity != null && activity.HasDeviceLockScreen();
            }
            catch
            {
                return false;
            }
#elif IOS
            try
            {
                var context = new LocalAuthentication.LAContext();
                return context.CanEvaluatePolicy(LocalAuthentication.LAPolicy.DeviceOwnerAuthentication, out _);
            }
            catch
            {
                return false;
            }
#elif WINDOWS
            try
            {
                var availability = Windows.Security.Credentials.UI.UserConsentVerifier.CheckAvailabilityAsync()
                    .AsTask().GetAwaiter().GetResult();
                return availability == Windows.Security.Credentials.UI.UserConsentVerifierAvailability.Available;
            }
            catch
            {
                return false;
            }
#else
            return true;
#endif
        });
    }

    public async Task<bool> AuthenticateWithDeviceLockAsync()
    {
        return await Task.Run(async () =>
        {
#if ANDROID
            try
            {
                var activity = MainActivity.Instance;
                if (activity == null)
                    return false;

                var result = await activity.AuthenticateWithDeviceLockAsync();
                if (result)
                    await SetAuthenticatedFromCurrentUserAsync();
                return result;
            }
            catch
            {
                return false;
            }
#elif IOS
            try
            {
                var context = new LocalAuthentication.LAContext();
                if (!context.CanEvaluatePolicy(LocalAuthentication.LAPolicy.DeviceOwnerAuthentication, out _))
                    return false;

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                context.EvaluatePolicy(
                    LocalAuthentication.LAPolicy.DeviceOwnerAuthentication,
                    "Verify your identity to continue",
                    (success, error) => tcs.TrySetResult(success));

                var result = await tcs.Task;
                if (result)
                    await SetAuthenticatedFromCurrentUserAsync();
                return result;
            }
            catch
            {
                return false;
            }
#elif WINDOWS
            try
            {
                var consentResult = await Windows.Security.Credentials.UI.UserConsentVerifier.RequestVerificationAsync("Verify your identity to continue");
                var result = consentResult == Windows.Security.Credentials.UI.UserConsentVerificationResult.Verified;
                if (result)
                    await SetAuthenticatedFromCurrentUserAsync();
                return result;
            }
            catch
            {
                return false;
            }
#else
            return true;
#endif
        });
    }
}