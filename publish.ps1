<#
.SYNOPSIS
    Automated publishing script for Revenue Operations Dashboard.

.DESCRIPTION
    Builds, publishes, and optionally packages & tags a release for Revenue Operations Dashboard.
    Supports interactive version prompts and default fallbacks.

.PARAMETER Version
    The release version (e.g. "v2.0.0", "v2"). If omitted, prompts interactively or defaults to "v2.0.0".

.PARAMETER Configuration
    The build configuration (Default: "Release").

.PARAMETER OutputDir
    Target directory for published output. Defaults to "./publish/<Version>".

.PARAMETER TagGit
    Whether to create and push a Git tag for this release (Default: $true).

.PARAMETER CreateZip
    Whether to create a compressed zip archive of the published output (Default: $true).
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Version,

    [Parameter()]
    [string]$Configuration = "Release",

    [Parameter()]
    [string]$OutputDir,

    [Parameter()]
    [switch]$TagGit = $true,

    [Parameter()]
    [switch]$CreateZip = $true
)

$ErrorActionPreference = "Stop"

# 1. Determine Root & Project Directory
$ScriptDir = $PSScriptRoot
if (-not $ScriptDir) { $ScriptDir = (Get-Location).Path }

$ProjectFile = Join-Path $ScriptDir "RevenueOperationsDashboard/RevenueOperationsDashboard.csproj"
if (-not (Test-Path $ProjectFile)) {
    $ProjectFile = Join-Path $ScriptDir "RevenueOperationsDashboard.csproj"
}

if (-not (Test-Path $ProjectFile)) {
    Write-Error "Project file not found at $ProjectFile"
    exit 1
}

# 2. Resolve Version (Prompt with fallback)
$DefaultVersion = "v2.0.0"
if ([string]::IsNullOrWhiteSpace($Version)) {
    try {
        $inputVersion = Read-Host "Enter release version [Default: $DefaultVersion]"
        if (-not [string]::IsNullOrWhiteSpace($inputVersion)) {
            $Version = $inputVersion.Trim()
        } else {
            $Version = $DefaultVersion
        }
    } catch {
        $Version = $DefaultVersion
    }
}

# Format version tag
if (-not $Version.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase) -and -not $Version.StartsWith("V")) {
    $Version = "v$Version"
}

# 3. Determine Output Directory
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $ScriptDir "publish/$Version"
}

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Revenue Operations Dashboard - Automated Publisher" -ForegroundColor White -BackgroundColor DarkBlue
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Project       : $ProjectFile" -ForegroundColor Gray
Write-Host "  Version       : $Version" -ForegroundColor Yellow
Write-Host "  Configuration : $Configuration" -ForegroundColor Gray
Write-Host "  Output Dir    : $OutputDir" -ForegroundColor Gray
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# 4. Clean & Prepare Target Output Directory
if (Test-Path $OutputDir) {
    Write-Host "Cleaning existing directory: $OutputDir..." -ForegroundColor DarkYellow
    Remove-Item -Path $OutputDir -Recurse -Force | Out-Null
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# 5. Execute dotnet publish
Write-Host "[1/3] Compiling and publishing ASP.NET Core application..." -ForegroundColor Green
$publishArgs = @(
    "publish",
    "`"$ProjectFile`"",
    "-c", $Configuration,
    "-o", "`"$OutputDir`"",
    "--nologo"
)

$publishCmd = "dotnet " + ($publishArgs -join " ")
Write-Host "Running: $publishCmd" -ForegroundColor DarkGray

$process = Start-Process -FilePath "dotnet" -ArgumentList $publishArgs -NoNewWindow -PassThru -Wait
if ($process.ExitCode -ne 0) {
    Write-Error "dotnet publish failed with exit code $($process.ExitCode)."
    exit $process.ExitCode
}

Write-Host "[OK] Build and publish completed successfully." -ForegroundColor Green

# 6. Create Zip Archive
$zipPath = $null
if ($CreateZip) {
    Write-Host "[2/3] Packaging release archive..." -ForegroundColor Green
    $publishParent = Split-Path -Parent $OutputDir
    $zipPath = Join-Path $publishParent "RevenueOperationsDashboard-$Version.zip"
    
    if (Test-Path $zipPath) {
        Remove-Item -Path $zipPath -Force
    }

    Compress-Archive -Path "$OutputDir/*" -DestinationPath $zipPath -CompressionLevel Optimal
    $zipSize = (Get-Item $zipPath).Length / 1MB
    Write-Host "[OK] Created package: $zipPath ($([Math]::Round($zipSize, 2)) MB)" -ForegroundColor Green
} else {
    Write-Host "[2/3] Skipping zip archive creation." -ForegroundColor DarkGray
}

# 7. Git Tagging & Remote Push
if ($TagGit) {
    Write-Host "[3/3] Creating and pushing Git tag '$Version'..." -ForegroundColor Green
    
    $gitExists = Get-Command "git" -ErrorAction SilentlyContinue
    if ($gitExists) {
        $existingTag = git tag -l $Version
        if ($existingTag) {
            Write-Host "Tag '$Version' already exists locally. Updating tag..." -ForegroundColor DarkYellow
            git tag -d $Version | Out-Null
        }

        git tag -a "$Version" -m "Release $Version - Executive Revenue Targets & Localization"
        Write-Host "[OK] Git tag '$Version' created." -ForegroundColor Green

        $hasRemote = git remote
        if ($hasRemote) {
            Write-Host "Pushing tag '$Version' to remote origin..." -ForegroundColor DarkCyan
            git push origin "$Version" --force
            Write-Host "[OK] Git tag '$Version' pushed to origin." -ForegroundColor Green
        } else {
            Write-Host "[!] No remote origin configured. Skipping tag push." -ForegroundColor Yellow
        }
    } else {
        Write-Host "[!] Git command not found. Skipping git tagging." -ForegroundColor Yellow
    }
} else {
    Write-Host "[3/3] Skipping Git tagging." -ForegroundColor DarkGray
}

# 8. Summary
Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  PUBLISH COMPLETED SUCCESSFULLY ($Version)" -ForegroundColor Black -BackgroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  Published Artifacts : $OutputDir" -ForegroundColor White
if ($zipPath -and (Test-Path $zipPath)) {
    Write-Host "  Release Package     : $zipPath" -ForegroundColor White
}
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
