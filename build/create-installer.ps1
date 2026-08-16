#!/usr/bin/env pwsh
# Markdown Viewer - MSI Installer Build Script
# Requires WiX Toolset v3.x or v4.x

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path $PSScriptRoot -Parent
$srcDir = Join-Path $projectRoot "src"
$publishDir = Join-Path $projectRoot "publish"
$installerDir = Join-Path $PSScriptRoot "installer"
$msiOutputDir = Join-Path $projectRoot "publish"

Write-Host "=== Markdown Viewer - MSI Installer Build ===" -ForegroundColor Cyan
Write-Host ""

# Check for WiX Toolset
$wixPath = $null
$wixVersion = $null

# Try WiX v7 (newer versions use wix.exe)
if (Test-Path "C:\Program Files\WiX Toolset v7.0\bin\wix.exe") {
    $wixPath = "C:\Program Files\WiX Toolset v7.0\bin\wix.exe"
    $wixVersion = "7"
}
# Try WiX v4
elseif (Test-Path "C:\Program Files\WiX Toolset v4\bin\wix.exe") {
    $wixPath = "C:\Program Files\WiX Toolset v4\bin\wix.exe"
    $wixVersion = "4"
}
# Try WiX v3
elseif (Test-Path "C:\Program Files (x86)\WiX Toolset v3\bin\candle.exe") {
    $wixPath = "C:\Program Files (x86)\WiX Toolset v3\bin"
    $wixVersion = "3"
}
# Fallback to %WIX% environment variable
elseif ($env:WIX -and (Test-Path "$env:WIX\bin\wix.exe")) {
    $wixPath = "$env:WIX\bin\wix.exe"
    $wixVersion = "7"
}
elseif ($env:WIX -and (Test-Path "$env:WIX\bin\candle.exe")) {
    $wixPath = "$env:WIX\bin"
    $wixVersion = "3"
}

if ($null -eq $wixPath) {
    Write-Host "WiX Toolset not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install WiX Toolset:" -ForegroundColor Yellow
    Write-Host "  - WiX v4: https://github.com/wixtoolset/wix4/releases" -ForegroundColor Yellow
    Write-Host "  - WiX v3: https://github.com/wixtoolset/wix3/releases" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Or use the portable ZIP package instead:" -ForegroundColor Cyan
    Write-Host "  .\build\publish.ps1" -ForegroundColor Cyan
    Write-Host ""
    exit 1
}

Write-Host "WiX Toolset v$wixVersion found: $wixPath" -ForegroundColor Green
Write-Host ""

# Clean output
if (Test-Path $msiOutputDir) {
    Remove-Item $msiOutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $msiOutputDir | Out-Null

# Publish first
Write-Host "Step 1: Publishing application..." -ForegroundColor Cyan
& dotnet publish "$srcDir/MarkdownViewer/MarkdownViewer.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -o "$publishDir\app"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Step 2: Building MSI installer..." -ForegroundColor Cyan

if ($wixVersion -eq "7" -or $wixVersion -eq "4") {
    # WiX v7/v4 (uses wix.exe build command)
    # Note: Run 'wix eula accept wix7' first to accept the OSMF EULA
    & $wixPath build "$installerDir\Product.wxs" `
        -d PublishDir="$publishDir\app" `
        -out "$msiOutputDir\MarkdownViewer-0.2.0-win-x64.msi"
} else {
    # WiX v3
    $intermediateDir = Join-Path $msiOutputDir "obj"
    New-Item -ItemType Directory -Path $intermediateDir | Out-Null
    
    & "$wixPath\candle.exe" `
        -d PublishDir="$publishDir\app" `
        -out "$intermediateDir\" `
        "$installerDir\Product.wxs"
    
    if ($LASTEXITCODE -ne 0) { exit 1 }
    
    & "$wixPath\light.exe" `
        -out "$msiOutputDir\MarkdownViewer-0.2.0-win-x64.msi" `
        "$intermediateDir\Product.wixobj"
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "MSI build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "MSI Installer: $msiOutputDir\MarkdownViewer-0.2.0-win-x64.msi" -ForegroundColor Cyan
Write-Host "Portable ZIP:  $publishDir\MarkdownViewer-0.2.0-win-x64.zip" -ForegroundColor Cyan
Write-Host ""
Write-Host "Distribution options:" -ForegroundColor Yellow
Write-Host "  1. MSI Installer (recommended for enterprise):" -ForegroundColor White
Write-Host "     - Run the .msi file to install" -ForegroundColor White
Write-Host "     - Creates Start Menu and Desktop shortcuts" -ForegroundColor White
Write-Host "     - Registers .md file association" -ForegroundColor White
Write-Host "     - Can be uninstalled via Windows Settings" -ForegroundColor White
Write-Host ""
Write-Host "  2. Portable ZIP (no installation required):" -ForegroundColor White
Write-Host "     - Extract and run MarkdownViewer.exe" -ForegroundColor White
Write-Host "     - No system changes" -ForegroundColor White
Write-Host "     - Ideal for USB drives or restricted environments" -ForegroundColor White
