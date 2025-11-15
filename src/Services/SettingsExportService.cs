using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace RobsYTDownloader.Services
{
    public class SettingsExportService
    {
        private readonly ConfigManager _configManager;

        public SettingsExportService()
        {
            _configManager = new ConfigManager();
        }

        /// <summary>
        /// Export all application settings, cookies, and history to a ZIP file
        /// </summary>
        /// <param name="destinationPath">Full path where to save the export ZIP file</param>
        /// <returns>True if export successful, false otherwise</returns>
        public async Task<(bool Success, string Message)> ExportSettings(string destinationPath)
        {
            try
            {
                // Create temporary directory for staging files
                var tempFolder = Path.Combine(Path.GetTempPath(), $"RobsYTDownloader_Export_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempFolder);

                try
                {
                    var appDataFolder = _configManager.ConfigFolder;
                    var appInstallFolder = AppDomain.CurrentDomain.BaseDirectory;

                    int exportedCount = 0;

                    // 1. Export config.json
                    var configFile = Path.Combine(appDataFolder, "config.json");
                    if (File.Exists(configFile))
                    {
                        await CopyFileAsync(configFile, Path.Combine(tempFolder, "config.json"));
                        exportedCount++;
                    }

                    // 2. Export cookies.txt
                    var cookiesFile = _configManager.GetCookiesFilePath();
                    if (File.Exists(cookiesFile))
                    {
                        await CopyFileAsync(cookiesFile, Path.Combine(tempFolder, "cookies.txt"));
                        exportedCount++;
                    }

                    // 3. Export download_history.json
                    var historyFile = Path.Combine(appDataFolder, "download_history.json");
                    if (File.Exists(historyFile))
                    {
                        await CopyFileAsync(historyFile, Path.Combine(tempFolder, "download_history.json"));
                        exportedCount++;
                    }

                    // 3a. Export video_link_history.json
                    var videoLinkHistoryFile = Path.Combine(appDataFolder, "video_link_history.json");
                    if (File.Exists(videoLinkHistoryFile))
                    {
                        await CopyFileAsync(videoLinkHistoryFile, Path.Combine(tempFolder, "video_link_history.json"));
                        exportedCount++;
                    }

                    // 4. Export oauth_config.json (check AppData first, then install folder)
                    var oauthConfigFileAppData = Path.Combine(appDataFolder, "oauth_config.json");
                    var oauthConfigFileInstall = Path.Combine(appInstallFolder, "oauth_config.json");

                    if (File.Exists(oauthConfigFileAppData))
                    {
                        await CopyFileAsync(oauthConfigFileAppData, Path.Combine(tempFolder, "oauth_config.json"));
                        exportedCount++;
                    }
                    else if (File.Exists(oauthConfigFileInstall))
                    {
                        // Fallback to install folder for backward compatibility
                        await CopyFileAsync(oauthConfigFileInstall, Path.Combine(tempFolder, "oauth_config.json"));
                        exportedCount++;
                    }

                    // Create export metadata
                    var metadata = new
                    {
                        ExportDate = DateTime.Now,
                        AppVersion = "1.2.1",
                        ExportedFiles = exportedCount,
                        Note = "Rob's YouTube Downloader Settings Export"
                    };

                    var metadataJson = Newtonsoft.Json.JsonConvert.SerializeObject(metadata, Newtonsoft.Json.Formatting.Indented);
                    await File.WriteAllTextAsync(Path.Combine(tempFolder, "export_info.json"), metadataJson);

                    // Create ZIP file
                    if (File.Exists(destinationPath))
                    {
                        File.Delete(destinationPath);
                    }

                    await Task.Run(() => ZipFile.CreateFromDirectory(tempFolder, destinationPath, CompressionLevel.Optimal, false));

                    return (true, $"Successfully exported {exportedCount} file(s) to:\n{destinationPath}");
                }
                finally
                {
                    // Clean up temp folder
                    if (Directory.Exists(tempFolder))
                    {
                        Directory.Delete(tempFolder, true);
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Export failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Import settings from a previously exported ZIP file
        /// </summary>
        /// <param name="importFilePath">Full path to the export ZIP file</param>
        /// <returns>True if import successful, false otherwise</returns>
        public async Task<(bool Success, string Message)> ImportSettings(string importFilePath)
        {
            try
            {
                if (!File.Exists(importFilePath))
                {
                    return (false, "Import file not found.");
                }

                // Create temporary directory for extraction
                var tempFolder = Path.Combine(Path.GetTempPath(), $"RobsYTDownloader_Import_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempFolder);

                try
                {
                    // Extract ZIP
                    await Task.Run(() => ZipFile.ExtractToDirectory(importFilePath, tempFolder));

                    var appDataFolder = _configManager.ConfigFolder;
                    var appInstallFolder = AppDomain.CurrentDomain.BaseDirectory;

                    int importedCount = 0;

                    // Verify this is a valid export by checking for metadata
                    var metadataFile = Path.Combine(tempFolder, "export_info.json");
                    if (!File.Exists(metadataFile))
                    {
                        return (false, "Invalid export file. Missing metadata.");
                    }

                    // Create backup of existing files before importing
                    await BackupCurrentSettings();

                    // 1. Import config.json
                    var configFile = Path.Combine(tempFolder, "config.json");
                    if (File.Exists(configFile))
                    {
                        await CopyFileAsync(configFile, Path.Combine(appDataFolder, "config.json"));
                        importedCount++;
                    }

                    // 2. Import cookies.txt
                    var cookiesFile = Path.Combine(tempFolder, "cookies.txt");
                    if (File.Exists(cookiesFile))
                    {
                        await CopyFileAsync(cookiesFile, _configManager.GetCookiesFilePath());
                        importedCount++;
                    }

                    // 3. Import download_history.json
                    var historyFile = Path.Combine(tempFolder, "download_history.json");
                    if (File.Exists(historyFile))
                    {
                        await CopyFileAsync(historyFile, Path.Combine(appDataFolder, "download_history.json"));
                        importedCount++;
                    }

                    // 3a. Import video_link_history.json
                    var videoLinkHistoryFile = Path.Combine(tempFolder, "video_link_history.json");
                    if (File.Exists(videoLinkHistoryFile))
                    {
                        await CopyFileAsync(videoLinkHistoryFile, Path.Combine(appDataFolder, "video_link_history.json"));
                        importedCount++;
                    }

                    // 4. Import oauth_config.json (to AppData, not install folder to avoid permission issues)
                    var oauthConfigFile = Path.Combine(tempFolder, "oauth_config.json");
                    if (File.Exists(oauthConfigFile))
                    {
                        await CopyFileAsync(oauthConfigFile, Path.Combine(appDataFolder, "oauth_config.json"));
                        importedCount++;
                    }

                    return (true, $"Successfully imported {importedCount} file(s).\n\nPlease restart the application for changes to take effect.");
                }
                finally
                {
                    // Clean up temp folder
                    if (Directory.Exists(tempFolder))
                    {
                        Directory.Delete(tempFolder, true);
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Import failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a backup of current settings before importing
        /// </summary>
        private async Task BackupCurrentSettings()
        {
            try
            {
                var appDataFolder = _configManager.ConfigFolder;
                var backupFolder = Path.Combine(appDataFolder, "backup");
                var timestampedBackup = Path.Combine(backupFolder, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}");

                Directory.CreateDirectory(timestampedBackup);

                var filesToBackup = new[]
                {
                    Path.Combine(appDataFolder, "config.json"),
                    _configManager.GetCookiesFilePath(),
                    Path.Combine(appDataFolder, "download_history.json")
                };

                foreach (var file in filesToBackup.Where(File.Exists))
                {
                    var fileName = Path.GetFileName(file);
                    await CopyFileAsync(file, Path.Combine(timestampedBackup, fileName));
                }

                // Keep only last 5 backups
                await CleanOldBackups(backupFolder, 5);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Backup warning: {ex.Message}");
                // Don't fail import if backup fails
            }
        }

        /// <summary>
        /// Clean old backup folders, keeping only the most recent N backups
        /// </summary>
        private async Task CleanOldBackups(string backupFolder, int keepCount)
        {
            await Task.Run(() =>
            {
                if (!Directory.Exists(backupFolder)) return;

                var backups = Directory.GetDirectories(backupFolder)
                    .Select(d => new DirectoryInfo(d))
                    .OrderByDescending(d => d.CreationTime)
                    .ToList();

                // Delete old backups beyond keepCount
                foreach (var oldBackup in backups.Skip(keepCount))
                {
                    try
                    {
                        oldBackup.Delete(true);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            });
        }

        /// <summary>
        /// Async file copy helper
        /// </summary>
        private async Task CopyFileAsync(string sourcePath, string destinationPath)
        {
            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            using var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await sourceStream.CopyToAsync(destStream);
        }

        /// <summary>
        /// Get size of exportable data in bytes
        /// </summary>
        public long GetExportableDataSize()
        {
            long totalSize = 0;

            try
            {
                var appDataFolder = _configManager.ConfigFolder;
                var appInstallFolder = AppDomain.CurrentDomain.BaseDirectory;

                var filesToCheck = new[]
                {
                    Path.Combine(appDataFolder, "config.json"),
                    _configManager.GetCookiesFilePath(),
                    Path.Combine(appDataFolder, "download_history.json"),
                    Path.Combine(appInstallFolder, "oauth_config.json")
                };

                foreach (var file in filesToCheck.Where(File.Exists))
                {
                    totalSize += new FileInfo(file).Length;
                }
            }
            catch
            {
                // Ignore errors
            }

            return totalSize;
        }
    }
}
