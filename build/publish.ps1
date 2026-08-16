#!/usr/bin/env pwsh
# Markdown Viewer - Publish Script
# Creates a self-contained deployment package

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path $PSScriptRoot -Parent
$srcDir = Join-Path $projectRoot "src"
$outputDir = Join-Path $projectRoot "publish"

Write-Host "=== Markdown Viewer - Publish ===" -ForegroundColor Cyan
Write-Host "Project root: $projectRoot"
Write-Host "Output: $outputDir"
Write-Host ""

# Clean previous publish
if (Test-Path $outputDir) {
    Write-Host "Cleaning previous publish..." -ForegroundColor Yellow
    Remove-Item $outputDir -Recurse -Force
}

# Publish self-contained
Write-Host "Publishing self-contained application..." -ForegroundColor Cyan
dotnet publish "$srcDir/MarkdownViewer/MarkdownViewer.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o "$outputDir/app"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# Create ZIP package
Write-Host "Creating ZIP package..." -ForegroundColor Cyan
$zipName = "MarkdownViewer-0.2.0-win-x64.zip"
$zipPath = Join-Path $outputDir $zipName

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    (Join-Path $outputDir "app"),
    $zipPath
)

Write-Host ""
Write-Host "=== Publish Complete ===" -ForegroundColor Green
Write-Host "Portable ZIP: $zipPath"
Write-Host "Extract folder: $(Join-Path $outputDir 'app')"
Write-Host ""
Write-Host "To install: Extract the ZIP and run MarkdownViewer.exe"
Write-Host "Optional: Create shortcut in Start Menu or Desktop"
