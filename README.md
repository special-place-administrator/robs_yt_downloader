# Rob's YouTube Downloader

A modern Windows 11-style YouTube downloader built with WPF (.NET 8.0) that supports downloading videos up to 8K HDR quality with Google OAuth authentication. Features a powerful concurrent download queue system with real-time progress tracking.

## Features

### Download Management
- **Download Queue System** (v1.0.7+) - Add multiple videos and download up to 3 simultaneously
- **Real-Time Progress Tracking** - Live progress bars with speed, ETA, and percentage for each download
- **Pause/Resume/Cancel** - Full control over individual downloads in the queue
- **Non-Blocking UI** - Continue browsing and adding videos while downloads run in background
- **Download History** - Track all your downloads with Open/Delete/Browse functionality

### Video Quality & Authentication
- **8K HDR Support** - Download videos up to 8K (7680x4320) with HDR support
- **Google OAuth Login** - Seamless authentication for accessing premium formats and members-only content
- **Format Filtering** - Filter by All/Video+Audio/Video Only/Audio Only formats
- **WebView2 Auto-Installer** - Automatically downloads and installs WebView2 runtime if needed

### Performance & UX
- **Multi-Connection Downloads** - Uses aria2c for accelerated downloads with configurable connections (4-20)
- **Responsive UI** (v1.0.8+) - Async operations prevent UI freezing
- **Modern Windows 11 UI** - Fluent Design with Mica-like effects and acrylic blur
- **In-App Notifications** - Toast-style notifications for status updates
- **Auto-Save Settings** - Settings automatically saved as you change them

## Requirements

- **Windows 10/11** (64-bit)
- **.NET 8.0 Runtime** - [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Node.js** (recommended) - Required for solving YouTube's n-signature challenges - [Download here](https://nodejs.org/)

### Optional Dependencies (Auto-installed by app)

- **yt-dlp** - Downloaded automatically on first use
- **aria2c** - Downloaded automatically on first use
- **ffmpeg** - Downloaded automatically on first use
- **WebView2 Runtime** - Automatically installed when you first log in with Google

## Installation

### Option 1: Windows Installer (Recommended)

1. Go to [Releases](https://github.com/special-place-administrator/robs_yt_downloader/releases)
2. Download the latest `RobsYTDownloader-Setup-v1.0.8.exe`
3. Run the installer
   - Choose installation directory
   - Installer will check for .NET 8.0 and Node.js
   - Creates desktop icon (optional)
   - Adds to Start Menu
   - Sets up PATH environment variable

**Latest Version:** v1.0.8
- ✅ Download queue with concurrent downloads
- ✅ Live progress tracking with pause/resume/cancel
- ✅ Responsive UI with no freezing

### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/special-place-administrator/robs_yt_downloader.git
cd robs_yt_downloader

# Build with .NET CLI
dotnet build --configuration Release

# Run the app
dotnet run --configuration Release
```

### Option 3: Build Installer from Source

**Prerequisites:**
- Inno Setup 6 or later: [Download here](https://jrsoftware.org/isdl.php)

**Build Steps:**

```powershell
# Build the installer
.\build_installer.ps1

# Or manually:
dotnet publish --configuration Release
iscc installer.iss
```

The installer will be created in `installer_output\RobsYTDownloader-Setup-v1.0.8.exe`

## Google OAuth Setup (Required for Login)

Before you can use the Google Login feature, you need to create your own Google OAuth application and configure the app with your credentials.

### Step 1: Create a Google OAuth App

1. Go to the [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable the YouTube Data API v3:
   - Go to "APIs & Services" > "Library"
   - Search for "YouTube Data API v3"
   - Click "Enable"
4. Create OAuth credentials:
   - Go to "APIs & Services" > "Credentials"
   - Click "Create Credentials" > "OAuth client ID"
   - If prompted, configure the OAuth consent screen first:
     - User Type: External
     - App name: Rob's YouTube Downloader (or any name)
     - User support email: Your email
     - Developer contact: Your email
     - Scopes: Add `https://www.googleapis.com/auth/youtube.readonly` and `https://www.googleapis.com/auth/userinfo.email`
   - Application type: "Desktop app"
   - Name: Rob's YouTube Downloader
   - Click "Create"
5. Download the credentials:
   - You'll see your Client ID and Client Secret
   - Keep these safe - you'll need them in the next step

### Step 2: Configure the Application

1. Navigate to the application directory (where `RobsYTDownloader.exe` is located)
2. Copy `oauth_config.json.template` to `oauth_config.json`:
   ```bash
   copy oauth_config.json.template oauth_config.json
   ```
3. Open `oauth_config.json` in a text editor
4. Replace the placeholder values with your actual Google OAuth credentials:
   - Replace `YOUR_GOOGLE_CLIENT_ID_HERE` with your Client ID
   - Replace `YOUR_GOOGLE_CLIENT_SECRET_HERE` with your Client Secret
5. Save the file

**IMPORTANT:** Never share your `oauth_config.json` file or commit it to version control. This file contains your private OAuth credentials.

### Example oauth_config.json

```json
{
  "GoogleOAuth": {
    "ClientId": "123456789-abcdefghijklmnopqrstuvwxyz.apps.googleusercontent.com",
    "ClientSecret": "GOCSPX-abcdefghijklmnopqrstuvwxyz",
    "RedirectUri": "http://localhost:8080/",
    "Scope": "https://www.googleapis.com/auth/youtube.readonly https://www.googleapis.com/auth/userinfo.email"
  }
}
```

## Usage

### First Time Setup

1. **Configure Google OAuth** - Follow the "Google OAuth Setup" section above to create and configure your OAuth credentials
2. **Launch the app** - On first launch, it will create necessary folders in `%APPDATA%\RobsYTDownloader`
3. **Login with Google** (optional but recommended):
   - Click Settings → Google Login → Login button
   - Complete the OAuth flow in the WebView2 browser
   - Your authentication cookies will be saved for future use
4. **Install Dependencies** (optional - app will auto-install when needed):
   - Go to Settings → Dependencies
   - Click "Install" for yt-dlp, aria2c, and ffmpeg

### Downloading Videos

1. **Paste YouTube URL** into the text box
2. **Click Fetch Qualities** to load available formats
3. **Optional: Filter formats** - Select All/Video+Audio/Video Only/Audio Only
4. **Select quality** from the dropdown (options include 8K HDR, 4K HDR, 1080p60, etc.)
5. **Click Download** - Video is instantly added to the download queue
6. **Repeat steps 1-5** to add more videos (up to 3 will download simultaneously)
7. **Monitor progress** - Each download shows live speed, ETA, and progress percentage
8. **Control downloads** - Use Pause/Resume/Cancel buttons for individual downloads

### Settings

- **Download Folder** - Set default download location
- **Max Connections** - Configure aria2c connection count (4-20 connections)
- **Google Login** - Authenticate to access premium formats
- **Dependencies** - Check status and install yt-dlp, aria2c, ffmpeg

### Download Queue & History

The download history table serves as both a queue manager and history viewer:

**Active Downloads (Queued/Downloading/Paused):**
- Live progress updates showing `45.2% - 1.5MB/s - ETA: 02:30`
- **Pause** - Temporarily pause an active download
- **Resume** - Continue a paused download
- **Cancel** - Cancel a queued or active download

**Completed Downloads:**
- **Open** - Launch downloaded file in default player
- **Folder** - Open file location in Explorer
- **Delete** - Remove file and clear from history

**Failed/Cancelled Downloads:**
- **Delete** - Clear from history

## Technical Details

### Architecture

- **Frontend**: WPF with ModernWpf for Windows 11 Fluent Design
- **OAuth**: Google OAuth 2.0 with WebView2 for authentication
- **Downloader**: yt-dlp with aria2c for multi-connection downloads
- **Video Processing**: ffmpeg for merging video/audio streams
- **Cookie Management**: Netscape cookie format for yt-dlp authentication

### Key Technologies

- **.NET 8.0** - Modern C# and WPF
- **ModernWpf (v0.9.6)** - Windows 11 UI controls
- **WebView2 (v1.0.2210.55)** - Embedded Chromium for OAuth
- **yt-dlp** - YouTube video extraction with EJS (External JavaScript) support
- **aria2c** - Multi-connection download accelerator
- **Node.js** - JavaScript runtime for solving YouTube's n-signature challenges

### File Locations

- **App Data**: `%APPDATA%\RobsYTDownloader\`
- **Configuration**: `%APPDATA%\RobsYTDownloader\config.json`
- **Cookies**: `%APPDATA%\RobsYTDownloader\cookies.txt`
- **Download History**: `%APPDATA%\RobsYTDownloader\history.json`
- **Dependencies**: `%APPDATA%\RobsYTDownloader\tools\`

## How It Works

### OAuth Authentication Flow

1. User clicks "Login with Google"
2. WebView2 opens with Google OAuth consent screen
3. User authorizes access to YouTube and email
4. App receives OAuth tokens and user email
5. WebView2 navigates to YouTube.com (session carries over)
6. Cookies are extracted from authenticated YouTube session
7. Cookies saved in Netscape format for yt-dlp

### Download Process (Queue System)

1. User adds video to queue by clicking Download
2. DownloadQueueManager manages up to 3 concurrent downloads using semaphore-based concurrency
3. For each download:
   - yt-dlp fetches video metadata using cookies
   - Node.js runtime solves YouTube's n-signature challenges
   - aria2c downloads video/audio streams with multiple connections
   - ffmpeg merges streams if necessary (for Video+Audio formats)
   - File validation ensures non-zero file size before marking complete
4. Progress updates parsed from yt-dlp output and displayed in real-time
5. Download state transitions: Queued → Downloading → Completed/Failed/Cancelled
6. History saved asynchronously to prevent UI blocking

## Troubleshooting

### No formats available / Only 360p showing

- **Solution**: Make sure you're logged in with Google (Settings → Google Login)
- **Reason**: High-quality formats require authentication

### "n challenge solving failed" error

- **Solution**: Install Node.js from https://nodejs.org/
- **Reason**: YouTube requires JavaScript runtime to decrypt video URLs

### WebView2 not found

- **Solution**: Click "Yes" when prompted to auto-install WebView2 Runtime
- **Alternative**: Download manually from https://go.microsoft.com/fwlink/p/?LinkId=2124703

### Download is slow

- **Solution**: Increase max connections in Settings (try 16 or 20)
- **Note**: Some videos may have throttling regardless of connection count

### Members-only videos won't download

- **Solution**: Log in with a Google account that has membership
- **Note**: App uses your YouTube Premium or channel membership status

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is open source and available under the MIT License.

## Acknowledgments

- [yt-dlp](https://github.com/yt-dlp/yt-dlp) - The excellent YouTube downloader
- [aria2](https://github.com/aria2/aria2) - High-speed download utility
- [ffmpeg](https://ffmpeg.org/) - Multimedia framework
- [ModernWpf](https://github.com/Kinnara/ModernWpf) - Modern UI for WPF

## Disclaimer

This tool is for personal use only. Please respect YouTube's Terms of Service and content creators' rights. Only download content you have permission to download.
