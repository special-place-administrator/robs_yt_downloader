using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using RobsYTDownloader.Models;

namespace RobsYTDownloader.Services
{
    public class DependencyManager
    {
        private readonly ConfigManager _configManager;
        private readonly HttpClient _httpClient;

        public DependencyManager()
        {
            _configManager = new ConfigManager();
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "RobsYTDownloader/1.0");
        }

        public async Task<DependencyStatus> CheckDependency(string toolName)
        {
            var status = new DependencyStatus { Name = toolName };

            try
            {
                // First check in system PATH
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = toolName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();

                    if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        status.IsInstalled = true;
                        status.Path = output.Split('\n')[0].Trim();
                        status.Version = await GetVersion(toolName, status.Path);
                        status.StatusText = $"Installed - {status.Version ?? "Version unknown"}";
                        return status;
                    }
                }

                // Check in local tools folder
                var localPath = GetLocalToolPath(toolName);
                if (File.Exists(localPath))
                {
                    status.IsInstalled = true;
                    status.Path = localPath;
                    status.Version = await GetVersion(toolName, localPath);
                    status.StatusText = $"Installed (Local) - {status.Version ?? "Version unknown"}";
                    return status;
                }

                status.IsInstalled = false;
                status.StatusText = "Not installed";
            }
            catch (Exception ex)
            {
                status.IsInstalled = false;
                status.StatusText = $"Error checking: {ex.Message}";
            }

            return status;
        }

        private async Task<string?> GetVersion(string toolName, string path)
        {
            try
            {
                var versionArg = toolName switch
                {
                    "yt-dlp" => "--version",
                    "aria2c" => "--version",
                    "ffmpeg" => "-version",
                    "node" => "--version",
                    _ => "--version"
                };

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = versionArg,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    var output = await process.StandardOutput.ReadToEndAsync();
                    return output.Split('\n')[0].Trim();
                }
            }
            catch
            {
                // Ignore version check errors
            }

            return null;
        }

        public async Task<bool> InstallDependency(string toolName)
        {
            try
            {
                var toolsFolder = _configManager.GetToolsFolder();

                return toolName switch
                {
                    "yt-dlp" => await InstallYtDlp(toolsFolder),
                    "aria2c" => await InstallAria2c(toolsFolder),
                    "ffmpeg" => await InstallFfmpeg(toolsFolder),
                    "node" => await InstallNode(toolsFolder),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error installing {toolName}: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> InstallYtDlp(string toolsFolder)
        {
            var exePath = Path.Combine(toolsFolder, "yt-dlp.exe");

            try
            {
                var url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(exePath, bytes);

                return File.Exists(exePath);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> InstallAria2c(string toolsFolder)
        {
            var aria2Folder = Path.Combine(toolsFolder, "aria2");
            var exePath = Path.Combine(aria2Folder, "aria2c.exe");

            try
            {
                // Download latest aria2 release
                var url = "https://github.com/aria2/aria2/releases/download/release-1.37.0/aria2-1.37.0-win-64bit-build1.zip";
                var zipPath = Path.Combine(toolsFolder, "aria2.zip");

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(zipPath, bytes);

                // Extract
                if (Directory.Exists(aria2Folder))
                    Directory.Delete(aria2Folder, true);

                ZipFile.ExtractToDirectory(zipPath, toolsFolder);

                // Find the extracted folder
                var extractedFolder = Directory.GetDirectories(toolsFolder, "aria2-*").FirstOrDefault();
                if (extractedFolder != null)
                {
                    Directory.Move(extractedFolder, aria2Folder);
                }

                File.Delete(zipPath);

                return File.Exists(exePath);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> InstallFfmpeg(string toolsFolder)
        {
            var ffmpegFolder = Path.Combine(toolsFolder, "ffmpeg");
            var exePath = Path.Combine(ffmpegFolder, "bin", "ffmpeg.exe");

            try
            {
                // Download ffmpeg essentials build
                var url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
                var zipPath = Path.Combine(toolsFolder, "ffmpeg.zip");

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(zipPath, bytes);

                // Extract
                if (Directory.Exists(ffmpegFolder))
                    Directory.Delete(ffmpegFolder, true);

                ZipFile.ExtractToDirectory(zipPath, toolsFolder);

                // Find the extracted folder
                var extractedFolder = Directory.GetDirectories(toolsFolder, "ffmpeg-*").FirstOrDefault();
                if (extractedFolder != null)
                {
                    Directory.Move(extractedFolder, ffmpegFolder);
                }

                File.Delete(zipPath);

                return File.Exists(exePath);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> InstallNode(string toolsFolder)
        {
            // For Node.js, we'll just direct the user to download it
            // as it requires a more complex installation process
            await Task.CompletedTask;

            var result = System.Windows.MessageBox.Show(
                "Node.js requires a full installation. Would you like to open the download page?",
                "Install Node.js",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://nodejs.org/en/download/",
                    UseShellExecute = true
                });
            }

            return false;
        }

        private string GetLocalToolPath(string toolName)
        {
            var toolsFolder = _configManager.GetToolsFolder();

            return toolName switch
            {
                "yt-dlp" => Path.Combine(toolsFolder, "yt-dlp.exe"),
                "aria2c" => Path.Combine(toolsFolder, "aria2", "aria2c.exe"),
                "ffmpeg" => Path.Combine(toolsFolder, "ffmpeg", "bin", "ffmpeg.exe"),
                "node" => Path.Combine(toolsFolder, "node", "node.exe"),
                _ => string.Empty
            };
        }

        public string? GetToolPath(string toolName)
        {
            var status = CheckDependency(toolName).Result;
            return status.IsInstalled ? status.Path : null;
        }
    }
}
