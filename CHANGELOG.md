# Changelog

All notable changes to Rob's YouTube Downloader will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.8] - 2025-11-15

### Fixed
- **CRITICAL:** Fixed UI freeze when clicking Download button
  - Root cause: Synchronous file I/O blocking UI thread in `DownloadHistoryManager`
  - Solution: Converted `SaveHistory()` to async `SaveHistoryAsync()`
  - JSON serialization now runs on thread pool via `Task.Run()`
  - File writes now use `File.WriteAllTextAsync()` for non-blocking I/O
  - User impact: Download button now responds instantly with no freeze

### Technical Details
- Changed `DownloadHistoryManager.SaveHistory()` to async implementation
- All history save operations now use fire-and-forget pattern `(_ = SaveHistoryAsync())`
- Prevents 1-2 second UI hang when adding videos to queue

## [1.0.7] - 2025-11-15

### Added - Major Feature: Download Queue System
- **Multiple concurrent downloads** - Download up to 3 videos simultaneously
- **Real-time progress tracking** - Live progress bars with %, speed, and ETA for each download
- **Queue management** - Add multiple videos to queue with automatic processing
- **Non-blocking UI** - Continue browsing and adding videos while downloads run in background
- **Download controls** - Pause, Resume, and Cancel buttons for each download
- **Status tracking** - Clear status for each download: Queued → Downloading → Completed

### Added - UI Components
- New `DownloadStatus` enum with states: Queued, Downloading, Paused, Completed, Failed, Cancelled
- Enhanced `DownloadHistoryItem` with `INotifyPropertyChanged` for reactive UI updates
- `DownloadStatusToVisibilityConverter` for dynamic button visibility based on download state
- Live progress display in Status column: `45.2% - 1.5MB/s - ETA: 02:30`
- Dynamic action buttons that change based on download state

### Added - Technical Implementation
- `DownloadQueueManager` service with semaphore-based concurrency control
- ObservableCollection for automatic UI updates
- Progress parsing from yt-dlp output using regex patterns
- Process and CancellationTokenSource references for download control
- File validation checking both existence and size before marking complete

### Fixed
- **0-byte download bug** - Downloads that failed but resulted in 0-byte files were incorrectly marked as "Completed"
- File validation now checks both file existence and size > 0
- Proper error detection and status reporting

### Changed
- Download button now instantly queues videos instead of blocking UI
- History table now serves as both queue manager and history viewer
- Status column widened from 100px to 250px to accommodate live progress
- Actions column widened from 180px to 200px for new button layout

### User Experience Improvements
**Before v1.0.7:**
1. Click Download
2. UI freezes
3. Wait for completion
4. Download next video

**After v1.0.7:**
1. Click Download → Instantly queued
2. Continue adding more videos
3. All download concurrently with live progress
4. Pause/Resume/Cancel as needed

## [1.0.6] - 2025-11-14

### Added
- Version number display in title bar: "Rob's YouTube Downloader - v1.0.6"

### Changed
- Title bar now shows current version for easy identification

## [1.0.5] - 2025-11-14

### Added
- Combined Video+Audio format options with automatic merging
- Format filtering: All Formats / Video+Audio / Video Only / Audio Only
- Automatic best audio selection when choosing video quality

### Changed
- No more manual format selection for combined streams
- Quality selection automatically includes audio merging where appropriate

## [1.0.4] - 2025-11-13

### Fixed
- Installer upgrade handling improvements
- Better detection of existing installations

## [1.0.3] - 2025-11-13

### Added
- URL sanitization for playlist links
- Extract video ID from various YouTube URL formats
- Support for youtube.com, youtu.be, m.youtube.com, embed URLs

### Fixed
- Playlist URLs now properly sanitized to single video
- Improved URL parsing with regex patterns

## [1.0.2] - 2025-11-12

### Fixed
- Bug fixes and stability improvements

## [1.0.1] - 2025-11-11

### Added
- First-time setup wizard for Google OAuth configuration
- Guided setup flow for new users
- Option to skip setup and configure later

### Changed
- Improved onboarding experience

## [1.0.0] - 2025-11-10

### Added - Initial Release
- Modern Windows 11 UI with Fluent Design
- 8K HDR video download support (up to 7680x4320)
- Google OAuth authentication for premium formats
- WebView2 auto-installer for OAuth flow
- Download history tracking
- Multi-connection downloads with aria2c (configurable 4-20 connections)
- Format selection with quality options
- In-app toast notifications
- Auto-save settings
- Node.js runtime support for solving YouTube's n-signature challenges
- Automatic dependency installation (yt-dlp, aria2c, ffmpeg)
- Cookie-based authentication for members-only content
- Real-time download progress with speed and ETA
- Settings panel for download folder, max connections, and dependencies

### Technical Stack
- .NET 8.0 WPF application
- ModernWpf (v0.9.6) for Windows 11 UI
- WebView2 (v1.0.2210.55) for OAuth
- Newtonsoft.Json for configuration
- yt-dlp for video extraction
- aria2c for accelerated downloads
- ffmpeg for stream merging

### File Locations
- App Data: `%APPDATA%\RobsYTDownloader\`
- Configuration: `config.json`
- Cookies: `cookies.txt`
- Download History: `history.json`
- Dependencies: `tools\`

---

## Version History Summary

- **v1.0.8** - UI freeze fix (async file operations)
- **v1.0.7** - Download queue system with concurrent downloads
- **v1.0.6** - Version display in title bar
- **v1.0.5** - Combined format options with auto-merging
- **v1.0.4** - Installer improvements
- **v1.0.3** - URL sanitization
- **v1.0.2** - Bug fixes
- **v1.0.1** - Setup wizard
- **v1.0.0** - Initial release

[1.0.8]: https://github.com/special-place-administrator/robs_yt_downloader/releases/tag/v1.0.8
[1.0.7]: https://github.com/special-place-administrator/robs_yt_downloader/releases/tag/v1.0.7
[1.0.6]: https://github.com/special-place-administrator/robs_yt_downloader/releases/tag/v1.0.6
