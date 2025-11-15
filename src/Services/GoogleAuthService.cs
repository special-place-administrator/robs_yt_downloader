using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace RobsYTDownloader.Services
{
    public class GoogleAuthService
    {
        private static GoogleAuthService? _instance;
        private static readonly object _lock = new object();

        private readonly ConfigManager _configManager;
        private string? _accessToken;
        private string? _refreshToken;
        private string? _userEmail;
        private bool _isAuthenticating = false;

        private GoogleAuthService()
        {
            _configManager = new ConfigManager();
            LoadStoredTokens();
        }

        public static GoogleAuthService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new GoogleAuthService();
                        }
                    }
                }
                return _instance;
            }
        }

        public Task<bool> AuthenticateAsync()
        {
            // Prevent multiple simultaneous authentication attempts
            if (_isAuthenticating)
            {
                System.Diagnostics.Debug.WriteLine("Authentication already in progress");
                return Task.FromResult(false);
            }

            _isAuthenticating = true;

            try
            {
                // Create and show auth window with WebView2
                var authWindow = new AuthWindow();
                var result = authWindow.ShowDialog();

                if (result == true || authWindow.AuthenticationSuccessful)
                {
                    // Store tokens and user info from auth window
                    _accessToken = authWindow.AccessToken;
                    _refreshToken = authWindow.RefreshToken;
                    _userEmail = authWindow.UserEmail;

                    // Save tokens
                    SaveTokens();

                    System.Diagnostics.Debug.WriteLine("Authentication successful via WebView2");
                    return Task.FromResult(true);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Authentication cancelled or failed");
                    return Task.FromResult(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Authentication error: {ex.Message}");
                return Task.FromResult(false);
            }
            finally
            {
                _isAuthenticating = false;
            }
        }


        private void SaveTokens()
        {
            try
            {
                var tokenData = new JObject
                {
                    ["access_token"] = _accessToken,
                    ["refresh_token"] = _refreshToken,
                    ["user_email"] = _userEmail
                };

                var tokenPath = Path.Combine(_configManager.ConfigFolder, "tokens.json");
                File.WriteAllText(tokenPath, tokenData.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving tokens: {ex.Message}");
            }
        }

        private void LoadStoredTokens()
        {
            try
            {
                var tokenPath = Path.Combine(_configManager.ConfigFolder, "tokens.json");
                if (File.Exists(tokenPath))
                {
                    var json = File.ReadAllText(tokenPath);
                    var tokenData = JObject.Parse(json);

                    _accessToken = tokenData["access_token"]?.ToString();
                    _refreshToken = tokenData["refresh_token"]?.ToString();
                    _userEmail = tokenData["user_email"]?.ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading tokens: {ex.Message}");
            }
        }

        public void Logout()
        {
            _accessToken = null;
            _refreshToken = null;
            _userEmail = null;

            try
            {
                var tokenPath = Path.Combine(_configManager.ConfigFolder, "tokens.json");
                if (File.Exists(tokenPath))
                {
                    File.Delete(tokenPath);
                }

                var cookiesPath = _configManager.GetCookiesFilePath();
                if (File.Exists(cookiesPath))
                {
                    File.Delete(cookiesPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during logout: {ex.Message}");
            }
        }

        public bool IsAuthenticated()
        {
            return !string.IsNullOrEmpty(_accessToken);
        }

        public string? GetUserEmail()
        {
            return _userEmail;
        }

        public string? GetRefreshToken()
        {
            return _refreshToken;
        }
    }
}
