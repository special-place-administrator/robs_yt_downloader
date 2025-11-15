using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;
using RobsYTDownloader.Services;

namespace RobsYTDownloader
{
    public partial class AuthWindow : Window
    {
        private string _clientId = "";
        private string _clientSecret = "";
        private string _redirectUri = "http://localhost:8080/";
        private string _scope = "https://www.googleapis.com/auth/youtube.readonly https://www.googleapis.com/auth/userinfo.email";

        private readonly ConfigManager _configManager;
        private readonly HttpClient _httpClient;
        private string? _authCode;
        private string? _state;
        private bool _authCompleted = false;

        public bool AuthenticationSuccessful { get; private set; } = false;
        public string? AccessToken { get; private set; }
        public string? RefreshToken { get; private set; }
        public string? UserEmail { get; private set; }

        public AuthWindow()
        {
            InitializeComponent();
            _configManager = new ConfigManager();
            _httpClient = new HttpClient();
            LoadOAuthConfig();
            Loaded += AuthWindow_Loaded;
        }

        private void LoadOAuthConfig()
        {
            try
            {
                // Check AppData first (preferred location, no admin rights needed)
                var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RobsYTDownloader");
                var configPathAppData = Path.Combine(appDataFolder, "oauth_config.json");

                // Fall back to install folder (for backward compatibility)
                var configPathInstall = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "oauth_config.json");

                string configPath;
                if (File.Exists(configPathAppData))
                {
                    configPath = configPathAppData;
                }
                else if (File.Exists(configPathInstall))
                {
                    configPath = configPathInstall;
                }
                else
                {
                    throw new FileNotFoundException(
                        "OAuth configuration file not found. Please copy oauth_config.json.template to oauth_config.json and fill in your Google OAuth credentials.\n\n" +
                        "See README.md for instructions on how to create a Google OAuth app.");
                }

                var json = File.ReadAllText(configPath);
                var config = JObject.Parse(json);

                _clientId = config["GoogleOAuth"]?["ClientId"]?.ToString() ?? throw new Exception("ClientId not found in oauth_config.json");
                _clientSecret = config["GoogleOAuth"]?["ClientSecret"]?.ToString() ?? throw new Exception("ClientSecret not found in oauth_config.json");
                _redirectUri = config["GoogleOAuth"]?["RedirectUri"]?.ToString() ?? _redirectUri;
                _scope = config["GoogleOAuth"]?["Scope"]?.ToString() ?? _scope;

                if (_clientId.Contains("YOUR_") || _clientSecret.Contains("YOUR_"))
                {
                    throw new Exception("Please replace placeholder values in oauth_config.json with your actual Google OAuth credentials.\n\nSee README.md for setup instructions.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"OAuth Configuration Error:\n\n{ex.Message}", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private async void AuthWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if WebView2 Runtime is installed
                if (!IsWebView2RuntimeInstalled())
                {
                    // Auto-download and install WebView2 Runtime
                    var result = MessageBox.Show(
                        "WebView2 Runtime is required for Google Login.\n\nWould you like to download and install it now? (About 2 MB download)",
                        "WebView2 Runtime Required",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        Close();
                        return;
                    }

                    // Download and install
                    var installed = await DownloadAndInstallWebView2();
                    if (!installed)
                    {
                        MessageBox.Show(
                            "Failed to install WebView2 Runtime. Please try again or install manually from:\nhttps://go.microsoft.com/fwlink/p/?LinkId=2124703",
                            "Installation Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        Close();
                        return;
                    }

                    MessageBox.Show(
                        "WebView2 Runtime installed successfully! Please restart the application for changes to take effect.",
                        "Installation Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    Close();
                    return;
                }

                // Initialize WebView2
                await AuthWebView.EnsureCoreWebView2Async();

                // Generate OAuth URL
                _state = Guid.NewGuid().ToString("N");
                var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                             $"client_id={_clientId}&" +
                             $"redirect_uri={Uri.EscapeDataString(_redirectUri)}&" +
                             $"response_type=code&" +
                             $"scope={Uri.EscapeDataString(_scope)}&" +
                             $"state={_state}&" +
                             $"access_type=offline&" +
                             $"prompt=consent";

                // Hide loading overlay
                LoadingOverlay.Visibility = Visibility.Collapsed;

                // Navigate to OAuth URL
                AuthWebView.CoreWebView2.Navigate(authUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing authentication: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private bool IsWebView2RuntimeInstalled()
        {
            try
            {
                // Check if WebView2 Runtime is installed by looking for registry keys
                var webView2Key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
                if (webView2Key == null)
                {
                    webView2Key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
                }

                if (webView2Key != null)
                {
                    webView2Key.Close();
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> DownloadAndInstallWebView2()
        {
            try
            {
                var tempPath = Path.GetTempPath();
                var installerPath = Path.Combine(tempPath, "MicrosoftEdgeWebview2Setup.exe");

                // Download WebView2 Runtime bootstrapper
                var url = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(installerPath, bytes);

                // Run the installer
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/silent /install",
                    UseShellExecute = true,
                    Verb = "runas" // Request admin elevation
                };

                using var process = System.Diagnostics.Process.Start(processStartInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();

                    // Clean up installer
                    try
                    {
                        File.Delete(installerPath);
                    }
                    catch { }

                    return process.ExitCode == 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error installing WebView2: {ex.Message}");
                return false;
            }
        }

        private async void AuthWebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (_authCompleted) return;

            try
            {
                var url = AuthWebView.CoreWebView2.Source;

                // Check if this is the OAuth callback
                if (url.StartsWith(_redirectUri))
                {
                    _authCompleted = true;

                    // Show loading overlay
                    LoadingOverlay.Visibility = Visibility.Visible;

                    // Parse the callback URL
                    var uri = new Uri(url);
                    var query = ParseQueryString(uri.Query);

                    // Check for errors
                    if (query.ContainsKey("error"))
                    {
                        var error = query["error"];
                        MessageBox.Show($"Authentication cancelled: {error}", "Authentication Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Close();
                        return;
                    }

                    // Extract code and state
                    _authCode = query.ContainsKey("code") ? query["code"] : null;
                    var returnedState = query.ContainsKey("state") ? query["state"] : null;

                    if (string.IsNullOrEmpty(_authCode) || returnedState != _state)
                    {
                        MessageBox.Show("Invalid authentication response", "Authentication Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        Close();
                        return;
                    }

                    // Exchange code for tokens
                    var tokenResult = await ExchangeCodeForTokens(_authCode);
                    if (!tokenResult)
                    {
                        MessageBox.Show("Failed to exchange authorization code", "Authentication Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        Close();
                        return;
                    }

                    // Get user info
                    await FetchUserInfo();

                    // Navigate directly to YouTube - the Google session will auto-login
                    // Set up a navigation completed handler
                    var youtubeLoaded = new TaskCompletionSource<bool>();
                    void OnYouTubeNavigated(object? s, CoreWebView2NavigationCompletedEventArgs args)
                    {
                        if (AuthWebView.CoreWebView2.Source.Contains("youtube.com"))
                        {
                            youtubeLoaded.TrySetResult(args.IsSuccess);
                        }
                    }

                    AuthWebView.CoreWebView2.NavigationCompleted += OnYouTubeNavigated;

                    try
                    {
                        // Just navigate to YouTube - the Google account session will carry over
                        AuthWebView.CoreWebView2.Navigate("https://www.youtube.com/");

                        // Wait for YouTube to load (with longer timeout for slow connections)
                        var loadTask = youtubeLoaded.Task;
                        var timeoutTask = Task.Delay(20000); // Increased to 20 seconds
                        var completedTask = await Task.WhenAny(loadTask, timeoutTask);

                        if (completedTask == loadTask && loadTask.Result)
                        {
                            // Give YouTube time to set all cookies
                            await Task.Delay(3000);

                            // Extract cookies from the authenticated session
                            await ExtractYouTubeCookies();

                            AuthenticationSuccessful = true;
                        }
                        else
                        {
                            // Still try to extract cookies even if navigation times out
                            System.Diagnostics.Debug.WriteLine("YouTube navigation timed out, trying to extract cookies anyway");
                            await ExtractYouTubeCookies();
                            AuthenticationSuccessful = true;
                        }
                    }
                    finally
                    {
                        AuthWebView.CoreWebView2.NavigationCompleted -= OnYouTubeNavigated;

                        // Stop any media playback before closing
                        try
                        {
                            await AuthWebView.CoreWebView2.ExecuteScriptAsync("document.querySelectorAll('video, audio').forEach(m => m.pause());");
                        }
                        catch { }
                    }

                    // Show success message after window operations are complete
                    if (AuthenticationSuccessful)
                    {
                        MessageBox.Show("Login successful! YouTube cookies have been extracted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
            }
        }

        private async Task<bool> ExchangeCodeForTokens(string code)
        {
            try
            {
                var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");

                var parameters = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret),
                    new KeyValuePair<string, string>("redirect_uri", _redirectUri),
                    new KeyValuePair<string, string>("grant_type", "authorization_code")
                };

                tokenRequest.Content = new FormUrlEncodedContent(parameters);

                var tokenResponse = await _httpClient.SendAsync(tokenRequest);
                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Token exchange failed: {tokenJson}");
                    return false;
                }

                var tokenData = JObject.Parse(tokenJson);
                AccessToken = tokenData["access_token"]?.ToString();
                RefreshToken = tokenData["refresh_token"]?.ToString();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Token exchange error: {ex.Message}");
                return false;
            }
        }

        private async Task FetchUserInfo()
        {
            try
            {
                if (string.IsNullOrEmpty(AccessToken))
                    return;

                var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
                request.Headers.Add("Authorization", $"Bearer {AccessToken}");

                var response = await _httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var userData = JObject.Parse(json);
                    UserEmail = userData["email"]?.ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching user info: {ex.Message}");
            }
        }

        private async Task ExtractYouTubeCookies()
        {
            try
            {
                var cookiesPath = _configManager.GetCookiesFilePath();

                // Collect cookies from ALL Google/YouTube domains
                var allCookies = new List<CoreWebView2Cookie>();
                var domains = new[]
                {
                    "https://www.youtube.com",
                    "https://youtube.com",
                    "https://www.google.com",
                    "https://google.com",
                    "https://accounts.google.com",
                    "https://accounts.youtube.com"
                };

                var seenCookies = new HashSet<string>(); // Track unique cookies by domain+name+path

                foreach (var domain in domains)
                {
                    var domainCookies = await AuthWebView.CoreWebView2.CookieManager.GetCookiesAsync(domain);
                    foreach (var cookie in domainCookies)
                    {
                        // Create unique key for this cookie
                        var key = $"{cookie.Domain}|{cookie.Name}|{cookie.Path}";
                        if (!seenCookies.Contains(key))
                        {
                            seenCookies.Add(key);
                            allCookies.Add(cookie);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Extracted {allCookies.Count} unique cookies from Google/YouTube domains");

                var cookies = allCookies;

                // Build Netscape cookie file format
                var cookieLines = new List<string>
                {
                    "# Netscape HTTP Cookie File",
                    "# This file was generated by RobsYTDownloader",
                    "# Edit at your own risk.",
                    ""
                };

                foreach (var cookie in cookies)
                {
                    // Netscape format: domain\tflag\tpath\tsecure\texpiration\tname\tvalue
                    var domain = cookie.Domain;
                    var flag = domain.StartsWith(".") ? "TRUE" : "FALSE";
                    var path = cookie.Path;
                    var secure = cookie.IsSecure ? "TRUE" : "FALSE";
                    // Convert DateTime to Unix timestamp
                    var expiration = cookie.Expires != DateTime.MinValue
                        ? new DateTimeOffset(cookie.Expires).ToUnixTimeSeconds().ToString()
                        : "0";
                    var name = cookie.Name;
                    var value = cookie.Value;

                    cookieLines.Add($"{domain}\t{flag}\t{path}\t{secure}\t{expiration}\t{name}\t{value}");
                }

                await File.WriteAllLinesAsync(cookiesPath, cookieLines);
                System.Diagnostics.Debug.WriteLine($"Extracted {cookies.Count} cookies to {cookiesPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting YouTube cookies: {ex.Message}");
            }
        }

        private Dictionary<string, string> ParseQueryString(string queryString)
        {
            var result = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(queryString))
                return result;

            // Remove leading '?'
            if (queryString.StartsWith("?"))
                queryString = queryString.Substring(1);

            var pairs = queryString.Split('&');
            foreach (var pair in pairs)
            {
                var parts = pair.Split('=');
                if (parts.Length == 2)
                {
                    result[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
                }
            }

            return result;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _httpClient?.Dispose();
            base.OnClosing(e);
        }
    }
}
