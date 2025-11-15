# Building Rob's YouTube Downloader

This guide explains how to build the application and create the Windows installer.

## Prerequisites

### Required
- **.NET 8.0 SDK**: [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Inno Setup 6**: [Download](https://jrsoftware.org/isdl.php) (only for building installer)

### Recommended
- **Node.js LTS**: [Download](https://nodejs.org/) - Required for 8K video support
- **Visual Studio 2022** or **VS Code** - For development

## Building the Application

### Quick Build

```powershell
# Build Debug version
dotnet build

# Build Release version
dotnet build --configuration Release

# Run the application
dotnet run --configuration Release
```

### Publish for Distribution

```powershell
# Publish self-contained (includes .NET runtime)
dotnet publish --configuration Release --self-contained true --runtime win-x64 --output ./publish

# Publish framework-dependent (smaller, requires .NET 8.0 installed)
dotnet publish --configuration Release --self-contained false --output ./publish
```

## Building the Installer

The installer uses **Inno Setup** to create a professional Windows installer with the following features:

### Installer Features

1. **User-Selected Installation Directory**
   - Default: `C:\Program Files\Rob's YouTube Downloader`
   - User can choose custom location

2. **Prerequisites Check**
   - Detects if .NET 8.0 Desktop Runtime is installed
   - Detects if Node.js is installed
   - Shows download links for missing prerequisites

3. **Environment Setup**
   - Adds application to Windows PATH
   - Creates `%APPDATA%\RobsYTDownloader` for app data
   - Creates `%APPDATA%\RobsYTDownloader\tools` for yt-dlp, aria2c, ffmpeg

4. **Shortcuts Creation**
   - Start Menu shortcut
   - Desktop icon (optional, user-selected during install)
   - Quick Launch icon (optional, Windows 7 only)

5. **Configuration Files**
   - Copies `oauth_config.json.template` to `oauth_config.json`
   - Preserves existing `oauth_config.json` on upgrade
   - Never uninstalls user data in AppData folder

6. **Uninstaller**
   - Professional uninstall experience
   - Removes application files
   - Removes registry entries and PATH modifications
   - Preserves user data (downloads, settings, cookies)

### Build Steps

#### Option 1: Using PowerShell Script (Recommended)

```powershell
# Build and create installer in one command
.\build_installer.ps1

# Skip rebuilding if Release build is current
.\build_installer.ps1 -SkipBuild
```

The script will:
1. Clean and build Release version
2. Publish the application
3. Compile the Inno Setup script
4. Output installer to `installer_output\RobsYTDownloader-Setup-v1.0.0.exe`

#### Option 2: Manual Build

```powershell
# Step 1: Build and publish Release version
dotnet clean --configuration Release
dotnet build --configuration Release
dotnet publish --configuration Release --output "bin\Release\net8.0-windows\publish"

# Step 2: Compile installer with Inno Setup
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss

# Installer will be created in: installer_output\
```

## Customizing the Installer

### Changing App Version

Edit `installer.iss` and update the version number:

```pascal
#define MyAppVersion "1.0.0"
```

### Adding Custom Icon

1. Create or obtain a `.ico` file with multiple sizes (16x16, 32x32, 48x48, 256x256)
2. Save it as `app_icon.ico` in the project root
3. The installer script will automatically use it

**Recommended tools for creating icons:**
- [IcoFX](https://icofx.ro/) (Free for non-commercial)
- [GIMP](https://www.gimp.org/) with ICO plugin
- Online: [Favicon.io](https://favicon.io/)

### Modifying Installation Paths

Edit `installer.iss`:

```pascal
DefaultDirName={autopf}\{#MyAppName}     ; Change installation directory
DefaultGroupName={#MyAppName}            ; Change Start Menu folder name
```

### Adding/Removing Files

Edit the `[Files]` section in `installer.iss`:

```pascal
[Files]
Source: "path\to\file"; DestDir: "{app}"; Flags: ignoreversion
```

## Installer Configuration Details

### App ID (GUID)

The installer uses a unique GUID to identify the application:

```pascal
AppId={{8F9E5A2C-1D3B-4C7E-9A8B-2F4E6D8C1A3B}
```

**Important:** Don't change this GUID after releasing the installer. It's used for:
- Detecting previous installations
- Upgrading existing installations
- Uninstalling the application

### Registry Keys

The installer creates the following registry entries:

- `HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\{GUID}` - Uninstall information
- `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment\Path` - Adds app to PATH

### Files Installed

**Application Files** (`C:\Program Files\Rob's YouTube Downloader\`):
- `RobsYTDownloader.exe` - Main executable
- `*.dll` - .NET runtime libraries and dependencies
- `oauth_config.json.template` - OAuth configuration template
- `oauth_config.json` - User's OAuth configuration (created from template)
- `README.md` - Documentation
- `LICENSE.txt` - License file

**User Data** (`%APPDATA%\RobsYTDownloader\`):
- `config.json` - Application settings
- `cookies.txt` - YouTube authentication cookies
- `history.json` - Download history
- `tools\` - yt-dlp, aria2c, ffmpeg (auto-downloaded by app)

## Testing the Installer

### Before Building

1. Ensure Release build works correctly
2. Test OAuth configuration loading
3. Verify all dependencies are included
4. Check that `oauth_config.json.template` is correct

### After Building

1. **Test on clean Windows VM** (recommended)
   - Windows 10 or Windows 11
   - Without .NET 8.0 or Node.js installed
   - Verify prerequisite detection works

2. **Test Installation**
   - Run installer
   - Verify prerequisite warnings show correctly
   - Test custom installation path
   - Check desktop icon creation
   - Verify Start Menu shortcuts

3. **Test Application**
   - Launch from desktop icon
   - Verify OAuth config prompts
   - Test basic download functionality

4. **Test Upgrade**
   - Build new version with different version number
   - Run installer over existing installation
   - Verify settings are preserved
   - Check oauth_config.json is not overwritten

5. **Test Uninstall**
   - Use Windows "Apps & Features"
   - Verify app is removed from Program Files
   - Verify PATH is cleaned up
   - Verify AppData folder is preserved

## Troubleshooting Build Issues

### "Inno Setup not found"

**Solution:** Install Inno Setup 6 from https://jrsoftware.org/isdl.php

### "Build failed" or "Publish failed"

**Solution:**
1. Clean solution: `dotnet clean`
2. Restore packages: `dotnet restore`
3. Try building again

### "app_icon.ico not found" warning

**Solution:** This is just a warning. The installer will use the default icon. You can:
- Ignore it (installer will still work)
- Create a custom icon and save as `app_icon.ico`

### Installer doesn't include all files

**Solution:**
1. Check the `[Files]` section in `installer.iss`
2. Make sure you published the app first: `dotnet publish`
3. Verify files exist in `bin\Release\net8.0-windows\`

### Prerequisite detection not working

**Solution:**
1. Ensure .NET 8.0 SDK is installed (not just runtime)
2. Verify `dotnet --list-runtimes` works from command line
3. Check Windows PATH includes .NET installation

## Release Checklist

Before creating a release:

- [ ] Update version number in `installer.iss`
- [ ] Update version number in `AssemblyInfo.cs` or `.csproj`
- [ ] Test build on clean system
- [ ] Create release notes
- [ ] Build installer: `.\build_installer.ps1`
- [ ] Test installer on Windows 10 and Windows 11
- [ ] Test upgrade from previous version
- [ ] Create GitHub release
- [ ] Upload installer to GitHub releases
- [ ] Update README with new version number

## Advanced: Signing the Installer

For production releases, consider code signing:

1. **Obtain a code signing certificate**
   - From certificate authorities like DigiCert, Sectigo
   - Or use self-signed for internal distribution

2. **Sign the installer**

```powershell
# Using SignTool (from Windows SDK)
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com installer_output\RobsYTDownloader-Setup-v1.0.0.exe
```

3. **Benefits of code signing:**
   - No Windows SmartScreen warnings
   - Users can verify publisher identity
   - Prevents tampering

## Contributing

If you make changes to the installer:

1. Test thoroughly on clean Windows installations
2. Document any new features in this file
3. Update version numbers appropriately
4. Test upgrade scenarios from previous versions

## Support

For issues with building or installer:
- Check existing issues: https://github.com/special-place-administrator/robs_yt_downloader/issues
- Create new issue with build logs
- Include OS version and .NET SDK version
