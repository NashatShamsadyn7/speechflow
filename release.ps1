# Builds and publishes a SpeechFlow release from this PC (no GitHub Actions needed).
#
#   .\release.ps1 -Version 3.0.1
#
# 1. Runs the tests.  2. Builds the single-file exe and the installer.
# 3. Tags the commit and creates the GitHub release (installer + portable exe).
# 4. Replaces SpeechFlow-Setup.exe on the website's download release.
# Then it prints the size and SHA-256 to put on the website.

param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [string]$Notes = "SpeechFlow $Version"
)

# Not 'Stop': Windows PowerShell treats git's normal progress output on stderr as an error.
# Every native command is checked with Check instead.
$ErrorActionPreference = 'Continue'
Set-Location $PSScriptRoot

function Step($text) { Write-Host "`n== $text" -ForegroundColor Cyan }
function Check($what) { if ($LASTEXITCODE -ne 0) { throw "$what failed (exit $LASTEXITCODE)" } }

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 is not installed (https://jrsoftware.org/isdl.php)." }
if (git status --porcelain) { throw "Commit or stash your changes first; a release must match a commit." }

Step "Testing"
dotnet test SpeechFlow.slnx -c Release; Check "Tests"

Step "Building $Version"
Remove-Item publish, dist -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish src/SpeechFlow/SpeechFlow.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:Version=$Version -o publish; Check "Publish"
& $iscc /Q "/DAppVersion=$Version" installer\SpeechFlow.iss; Check "Installer"

$setup = "dist\SpeechFlow-Setup-$Version.exe"
$portable = "dist\SpeechFlow-Portable-$Version.exe"
Copy-Item publish\SpeechFlow.exe $portable

Step "GitHub release v$Version"
git tag "v$Version"; Check "Tag"
git push origin HEAD "v$Version"; Check "Push"
gh release create "v$Version" $setup $portable --title "SpeechFlow $Version" --notes $Notes; Check "Release"

Step "Website download"
$siteCopy = Join-Path $env:TEMP "SpeechFlow-Setup.exe"
Copy-Item $setup $siteCopy -Force
gh release upload v2026.08.04 $siteCopy -R NashatShamsadyn7/app-downloads --clobber; Check "Upload"

$size = "{0:N1} MB" -f ((Get-Item $setup).Length / 1MB)
$hash = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLower()
Write-Host "`nDone. Update portfolio-website\index.html (SpeechFlow card) and app-updates.json:" -ForegroundColor Green
Write-Host "  Version  $Version"
Write-Host "  Size     $size"
Write-Host "  SHA-256  $hash"
