using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RobsYTDownloader.Models;

namespace RobsYTDownloader.Services
{
    public class DownloadProgressEventArgs : EventArgs
    {
        public double Percentage { get; set; }
        public string Speed { get; set; } = string.Empty;
        public string ETA { get; set; } = string.Empty;
    }

    public class DownloadManager
    {
        private readonly DependencyManager _dependencyManager;
        private readonly ConfigManager _configManager;

        public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;
        public event EventHandler<string>? StatusChanged;

        public DownloadManager()
        {
            _dependencyManager = new DependencyManager();
            _configManager = new ConfigManager();
        }

        public async Task<VideoInfo> FetchFormats(string url)
        {
            var ytDlpPath = _dependencyManager.GetToolPath("yt-dlp");
            if (string.IsNullOrEmpty(ytDlpPath))
            {
                throw new Exception("yt-dlp is not installed. Please go to Settings → Dependencies and install it.");
            }

            var formats = new List<VideoFormat>();

            try
            {
                StatusChanged?.Invoke(this, "Connecting to YouTube servers...");
                await Task.Delay(100); // Give UI time to update

                var argumentsList = new List<string>();

                // Use cookies for authentication (OAuth is no longer supported by yt-dlp)
                var cookiesPath = _configManager.GetCookiesFilePath();
                if (System.IO.File.Exists(cookiesPath))
                {
                    argumentsList.Add($"--cookies \"{cookiesPath}\"");
                }

                // Check if Node.js is available for JavaScript challenge solving
                var nodePath = _dependencyManager.GetToolPath("node");
                if (!string.IsNullOrEmpty(nodePath))
                {
                    // Enable Node.js runtime for solving YouTube's n-signature challenges
                    argumentsList.Add("--js-runtimes node");
                }

                argumentsList.Add("-J");
                argumentsList.Add("--no-warnings");
                argumentsList.Add($"\"{url}\"");

                var arguments = string.Join(" ", argumentsList);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = ytDlpPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    throw new Exception("Failed to start yt-dlp. Please check if yt-dlp is properly installed.");
                }

                StatusChanged?.Invoke(this, "Fetching video information...");

                // Simple timeout with Task.WhenAny
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                var timeout = TimeSpan.FromSeconds(20);
                var processTask = process.WaitForExitAsync();
                var timeoutTask = Task.Delay(timeout);

                var completedTask = await Task.WhenAny(processTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    StatusChanged?.Invoke(this, "Operation timed out. Cancelling...");
                    try { process.Kill(); } catch { }
                    throw new Exception("Request timed out after 20 seconds.\n\nPossible causes:\n• No internet connection\n• Invalid YouTube URL\n• yt-dlp needs updating\n• Firewall blocking access");
                }

                var output = await outputTask;
                var error = await errorTask;

                if (process.ExitCode != 0)
                {
                    var errorMsg = string.IsNullOrWhiteSpace(error) ? "Unknown error occurred" : error;

                    // Parse common errors
                    if (errorMsg.Contains("Video unavailable"))
                        throw new Exception("This video is unavailable or has been removed.");
                    if (errorMsg.Contains("Private video"))
                        throw new Exception("This video is private and cannot be downloaded.");
                    if (errorMsg.Contains("members-only"))
                        throw new Exception("This is a members-only video. Login required.");

                    throw new Exception($"Failed to fetch video information:\n{errorMsg}");
                }

                StatusChanged?.Invoke(this, "Parsing available formats...");
                await Task.Delay(100).ConfigureAwait(false);

                var json = JObject.Parse(output);

                // Extract video metadata
                var videoTitle = json["title"]?.ToString() ?? "video";
                var videoId = json["id"]?.ToString() ?? "unknown";

                var formatsArray = json["formats"] as JArray;

                if (formatsArray == null)
                {
                    throw new Exception("No formats found in video info");
                }

                // Filter and parse formats
                foreach (var formatToken in formatsArray)
                {
                    var format = formatToken as JObject;
                    if (format == null) continue;

                    var formatId = format["format_id"]?.ToString();
                    var ext = format["ext"]?.ToString();
                    var vcodec = format["vcodec"]?.ToString();
                    var acodec = format["acodec"]?.ToString();
                    var resolution = format["resolution"]?.ToString();
                    var width = format["width"]?.ToObject<int?>();
                    var height = format["height"]?.ToObject<int?>();
                    var fps = format["fps"]?.ToObject<double?>();
                    var filesize = format["filesize"]?.ToObject<long?>();
                    var dynamicRange = format["dynamic_range"]?.ToString();

                    if (string.IsNullOrEmpty(formatId)) continue;

                    // Check if format has video or audio
                    var hasVideo = !string.IsNullOrEmpty(vcodec) && vcodec != "none";
                    var hasAudio = !string.IsNullOrEmpty(acodec) && acodec != "none";

                    // Skip formats with neither video nor audio
                    if (!hasVideo && !hasAudio) continue;

                    // Use height for resolution if resolution string is missing or unclear
                    if (string.IsNullOrEmpty(resolution) || resolution == "audio only")
                    {
                        if (height.HasValue && height.Value > 0)
                        {
                            resolution = $"{height}p";
                        }
                        else if (resolution == "audio only")
                        {
                            resolution = "audio only";
                        }
                        else
                        {
                            resolution = "Unknown";
                        }
                    }

                    var displayName = BuildDisplayName(resolution, ext, fps, dynamicRange, hasVideo, hasAudio);

                    formats.Add(new VideoFormat
                    {
                        FormatId = formatId,
                        DisplayName = displayName,
                        Extension = ext ?? "mp4",
                        Resolution = resolution ?? "Unknown",
                        VideoCodec = vcodec ?? "none",
                        AudioCodec = acodec ?? "none",
                        FileSize = filesize,
                        Fps = fps,
                        Hdr = dynamicRange
                    });
                }

                // Sort formats by quality (resolution height) in descending order
                formats = formats.OrderByDescending(f =>
                {
                    // Extract numeric resolution (e.g., "4320p" -> 4320)
                    var resStr = f.Resolution;
                    if (resStr == "audio only") return -1;
                    if (resStr == "Unknown") return -2;

                    var match = System.Text.RegularExpressions.Regex.Match(resStr, @"(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int height))
                    {
                        return height;
                    }
                    return 0;
                }).ToList();

                // Also add "best" option
                formats.Insert(0, new VideoFormat
                {
                    FormatId = "bestvideo+bestaudio/best",
                    DisplayName = "Best Quality (Auto)",
                    Extension = "mp4",
                    Resolution = "Best Available"
                });

                StatusChanged?.Invoke(this, $"Found {formats.Count} formats");

                return new VideoInfo
                {
                    Title = videoTitle,
                    VideoId = videoId,
                    Formats = formats
                };
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Error: {ex.Message}");
                throw;
            }
        }

        private string BuildDisplayName(string? resolution, string? ext, double? fps, string? hdr, bool hasVideo, bool hasAudio)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(resolution) && resolution != "audio only")
            {
                parts.Add(resolution);
            }

            if (fps.HasValue && fps.Value > 30)
            {
                parts.Add($"{fps.Value}fps");
            }

            if (!string.IsNullOrEmpty(hdr) && hdr != "SDR")
            {
                parts.Add(hdr);
            }

            if (!hasAudio)
            {
                parts.Add("(Video Only)");
            }
            else if (!hasVideo)
            {
                parts.Add("(Audio Only)");
            }

            if (!string.IsNullOrEmpty(ext))
            {
                parts.Add($"[{ext}]");
            }

            return parts.Count > 0 ? string.Join(" ", parts) : "Unknown Format";
        }

        public async Task DownloadVideo(string url, string formatId, string outputPath)
        {
            var ytDlpPath = _dependencyManager.GetToolPath("yt-dlp");
            if (string.IsNullOrEmpty(ytDlpPath))
            {
                throw new Exception("yt-dlp is not installed. Please install it from Settings.");
            }

            var aria2cPath = _dependencyManager.GetToolPath("aria2c");
            var ffmpegPath = _dependencyManager.GetToolPath("ffmpeg");

            try
            {
                StatusChanged?.Invoke(this, "Starting download...");

                var config = _configManager.LoadConfig();
                var maxConnections = config.MaxConnections > 0 ? config.MaxConnections : 16;

                var arguments = new List<string>();

                // Add cookies if available
                var cookiesPath = _configManager.GetCookiesFilePath();
                if (System.IO.File.Exists(cookiesPath))
                {
                    arguments.Add($"--cookies \"{cookiesPath}\"");
                }

                // Check if Node.js is available for JavaScript challenge solving
                var nodePath = _dependencyManager.GetToolPath("node");
                if (!string.IsNullOrEmpty(nodePath))
                {
                    // Enable Node.js runtime for solving YouTube's n-signature challenges
                    arguments.Add("--js-runtimes node");
                }

                arguments.Add($"-f \"{formatId}\"");
                arguments.Add($"-o \"{outputPath}\"");
                arguments.Add("--newline");
                arguments.Add("--no-warnings");

                // Add aria2c if available
                if (!string.IsNullOrEmpty(aria2cPath))
                {
                    arguments.Add($"--external-downloader aria2c");
                    arguments.Add($"--external-downloader-args \"-x {maxConnections} -k 1M\"");
                }

                // Add ffmpeg path if available
                if (!string.IsNullOrEmpty(ffmpegPath))
                {
                    var ffmpegDir = System.IO.Path.GetDirectoryName(ffmpegPath);
                    arguments.Add($"--ffmpeg-location \"{ffmpegDir}\"");
                }

                arguments.Add($"\"{url}\"");

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = ytDlpPath,
                    Arguments = string.Join(" ", arguments),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    throw new Exception("Failed to start download process");
                }

                // Read output line by line
                while (!process.StandardOutput.EndOfStream)
                {
                    var line = await process.StandardOutput.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) continue;

                    ParseProgressLine(line);
                }

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    throw new Exception($"Download failed: {error}");
                }

                StatusChanged?.Invoke(this, "Download completed successfully!");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Error: {ex.Message}");
                throw;
            }
        }

        private void ParseProgressLine(string line)
        {
            // Parse yt-dlp progress output
            // Format: [download]  45.0% of 123.45MiB at 1.23MiB/s ETA 00:45
            var progressMatch = Regex.Match(line, @"\[download\]\s+(\d+\.?\d*)%.*?at\s+(\S+)\s+ETA\s+(\S+)");

            if (progressMatch.Success)
            {
                if (double.TryParse(progressMatch.Groups[1].Value, out double percentage))
                {
                    var speed = progressMatch.Groups[2].Value;
                    var eta = progressMatch.Groups[3].Value;

                    ProgressChanged?.Invoke(this, new DownloadProgressEventArgs
                    {
                        Percentage = percentage,
                        Speed = speed,
                        ETA = eta
                    });
                }
            }
            else if (line.Contains("[download]"))
            {
                StatusChanged?.Invoke(this, line.Replace("[download]", "").Trim());
            }
        }
    }
}
