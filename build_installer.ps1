# Build Installer Script for Rob's YouTube Downloader
# This script builds the Release version and creates the installer

param(
    [switch]$SkipBuild = $false
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Rob's YouTube Downloader - Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Inno Setup is installed
$InnoSetupPaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$ISCC = $null
foreach ($path in $InnoSetupPaths) {
    if (Test-Path $path) {
        $ISCC = $path
        break
    }
}

if (-not $ISCC) {
    Write-Host "ERROR: Inno Setup not found!" -ForegroundColor Red
    Write-Host "Please install Inno Setup 6 from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    exit 1
}

Write-Host "Found Inno Setup at: $ISCC" -ForegroundColor Green
Write-Host ""

# Build the Release version
if (-not $SkipBuild) {
    Write-Host "Step 1: Building Release version..." -ForegroundColor Cyan
    dotnet clean --configuration Release
    dotnet build --configuration Release

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Build failed!" -ForegroundColor Red
        exit 1
    }

    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "Skipping build (using existing Release build)..." -ForegroundColor Yellow
    Write-Host ""
}

# Publish the application
Write-Host "Step 2: Publishing application..." -ForegroundColor Cyan
dotnet publish --configuration Release --output "bin\Release\net8.0-windows\publish" --self-contained false

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Publish completed successfully!" -ForegroundColor Green
Write-Host ""

# Check if app icon exists, if not create a placeholder
if (-not (Test-Path "app_icon.ico")) {
    Write-Host "WARNING: app_icon.ico not found. The installer will use default icon." -ForegroundColor Yellow
    Write-Host "You can create a custom icon and save it as app_icon.ico" -ForegroundColor Yellow
    Write-Host ""
}

# Create installer output directory
if (-not (Test-Path "installer_output")) {
    New-Item -ItemType Directory -Path "installer_output" | Out-Null
}

# Build the installer
Write-Host "Step 3: Building installer..." -ForegroundColor Cyan
& $ISCC "installer.iss"

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Installer build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "SUCCESS! Installer built successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Installer location: installer_output\RobsYTDownloader-Setup-v1.0.0.exe" -ForegroundColor Cyan
Write-Host ""
Write-Host "The installer includes:" -ForegroundColor White
Write-Host "  - Automatic .NET 8.0 Runtime installation (if needed)" -ForegroundColor Gray
Write-Host "  - Automatic Node.js installation (if needed)" -ForegroundColor Gray
Write-Host "  - Desktop icon creation (optional)" -ForegroundColor Gray
Write-Host "  - Start menu shortcuts" -ForegroundColor Gray
Write-Host "  - Adds app to PATH" -ForegroundColor Gray
Write-Host "  - Creates AppData folders" -ForegroundColor Gray
Write-Host ""
