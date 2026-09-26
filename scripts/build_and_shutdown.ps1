<#
.SYNOPSIS
    Builds GLASSCORE completely on this PC and then shuts the PC down.

.DESCRIPTION
    1. Runs the automated tests (if the .NET 8 SDK is installed).
    2. Publishes the headless dedicated server  -> Build\Server\GlasscoreServer.exe
    3. Builds the Unity game (batch mode)        -> Build\Windows\GLASSCORE.exe
    4. Writes Build\BUILD_REPORT.txt and a copy on your Desktop.
    5. Shuts Windows down ONLY AFTER everything above has finished (default: 120 s grace period).
       Cancel a pending shutdown at any time with:   shutdown /a

.PARAMETER UnityPath
    Full path to Unity.exe. Auto-detected from Unity Hub installs when omitted (prefers Unity 6).
.PARAMETER NoShutdown
    Build only, keep the PC running.
.PARAMETER ShutdownOnlyOnSuccess
    Keep the PC running if the Unity build failed (so you can read the log).
.PARAMETER ShutdownDelaySeconds
    Grace period before power-off (default 120).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\build_and_shutdown.ps1
#>
param(
    [string]$UnityPath,
    [switch]$NoShutdown,
    [switch]$ShutdownOnlyOnSuccess,
    [int]$ShutdownDelaySeconds = 120,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Continue'
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root 'Glasscore'
$BuildDir = Join-Path $Root 'Build'
New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null
$ReportPath = Join-Path $BuildDir 'BUILD_REPORT.txt'
$script:Lines = New-Object System.Collections.Generic.List[string]
$Started = Get-Date

function Log([string]$Message) {
    $line = '[{0:HH:mm:ss}] {1}' -f (Get-Date), $Message
    Write-Host $line
    $script:Lines.Add($line)
}

function Find-Unity {
    if ($UnityPath) {
        if (Test-Path $UnityPath) { return $UnityPath }
        Log "UnityPath '$UnityPath' does not exist - trying auto-detection."
    }
    $roots = @()
    foreach ($pair in @(@($env:ProgramFiles, 'Unity\Hub\Editor'), @(${env:ProgramFiles(x86)}, 'Unity\Hub\Editor'), @($env:LOCALAPPDATA, 'Programs\Unity\Hub\Editor'))) {
        if ($pair[0]) { $roots += (Join-Path $pair[0] $pair[1]) }
    }
    $secondary = if ($env:APPDATA) { Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json' } else { $null }
    if ($secondary -and (Test-Path $secondary)) {
        $custom = (Get-Content $secondary -Raw).Trim().Trim('"')
        if ($custom) { $roots += $custom }
    }
    $found = @()
    foreach ($r in $roots) {
        if (-not $r -or -not (Test-Path $r)) { continue }
        Get-ChildItem $r -Directory | ForEach-Object {
            $exe = Join-Path $_.FullName 'Editor\Unity.exe'
            if (Test-Path $exe) {
                $numeric = ($_.Name -replace '[a-zA-Z].*$', '')
                $ver = $null
                [void][version]::TryParse($numeric, [ref]$ver)
                $found += [pscustomobject]@{ Name = $_.Name; Version = $ver; Exe = $exe; IsUnity6 = ($_.Name -like '6000.*') }
            }
        }
    }
    if ($found.Count -eq 0) { return $null }
    $best = $found | Sort-Object -Property @{ Expression = 'IsUnity6'; Descending = $true }, @{ Expression = 'Version'; Descending = $true } | Select-Object -First 1
    Log "Using Unity $($best.Name)"
    return $best.Exe
}

Log '=== GLASSCORE unattended build ==='
Log "Repository: $Root"

# ── 1) tests + dedicated server (optional, needs the .NET 8 SDK) ───────────────────────────
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$testsOk = $null
$serverOk = $null
if ($dotnet) {
    if (-not $SkipTests) {
        Log 'Running automated tests (simulation, netcode, loopback match)...'
        & dotnet test (Join-Path $Root 'tests\Glasscore.Tests') -c Release --nologo 2>&1 | Tee-Object -FilePath (Join-Path $BuildDir 'tests.log') | Out-Null
        $testsOk = ($LASTEXITCODE -eq 0)
        Log ("Tests: " + $(if ($testsOk) { 'PASSED' } else { 'FAILED (see Build\tests.log)' }))
    }
    Log 'Publishing dedicated server...'
    & dotnet publish (Join-Path $Root 'server\Glasscore.Server') -c Release -o (Join-Path $BuildDir 'Server') --nologo 2>&1 | Tee-Object -FilePath (Join-Path $BuildDir 'server_publish.log') | Out-Null
    $serverOk = ($LASTEXITCODE -eq 0)
    Log ("Dedicated server: " + $(if ($serverOk) { 'OK -> Build\Server\GlasscoreServer.exe' } else { 'FAILED (see Build\server_publish.log)' }))
    Log 'Publishing standalone desktop client (raylib)...'
    & dotnet publish (Join-Path $Root 'desktop\Glasscore.Desktop') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $BuildDir 'Desktop') --nologo 2>&1 | Tee-Object -FilePath (Join-Path $BuildDir 'desktop_publish.log') | Out-Null
    Log ("Desktop client: " + $(if ($LASTEXITCODE -eq 0) { 'OK -> Build\Desktop\GLASSCORE.exe' } else { 'FAILED (see Build\desktop_publish.log)' }))
} else {
    Log '.NET SDK not found - skipping tests, the standalone dedicated server and the desktop client.'
}

# ── 2) Unity build ─────────────────────────────────────────────────────────────────────────
$unity = Find-Unity
$gameOk = $false
$unityLog = Join-Path $BuildDir 'unity_build.log'
$exePath = Join-Path $BuildDir 'Windows\GLASSCORE.exe'
if (-not $unity) {
    Log 'ERROR: No Unity editor found. Install Unity 6 (6000.x LTS) with "Windows Build Support (Mono)" via Unity Hub, or pass -UnityPath.'
} else {
    Log 'Building the game with Unity (first import can take several minutes)...'
    $unityArgs = @(
        '-batchmode', '-quit',
        '-projectPath', "`"$Project`"",
        '-executeMethod', 'Glasscore.EditorTools.GlasscoreBuild.BuildWindowsCli',
        '-glasscoreOutput', "`"$exePath`"",
        '-logFile', "`"$unityLog`""
    )
    $proc = Start-Process -FilePath $unity -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
    $gameOk = ($proc.ExitCode -eq 0) -and (Test-Path $exePath)
    if ($gameOk) {
        Log "Game build: OK -> $exePath"
    } else {
        Log "Game build: FAILED (Unity exit code $($proc.ExitCode)). Details: $unityLog"
        if (Test-Path $unityLog) {
            $errors = Select-String -Path $unityLog -Pattern 'error CS\d+|Build Failed|No valid Unity Editor license|Aborting batchmode' | Select-Object -First 25
            foreach ($e in $errors) { Log ('  ' + $e.Line.Trim()) }
        }
    }
}

# ── 3) report ──────────────────────────────────────────────────────────────────────────────
$duration = (Get-Date) - $Started
Log ("Finished in {0:hh\:mm\:ss}." -f $duration)
$summary = if ($gameOk) { 'SUCCESS - GLASSCORE is ready: Build\Windows\GLASSCORE.exe' } else { 'FAILED - see the log lines above' }
Log "RESULT: $summary"
$script:Lines | Set-Content -Path $ReportPath -Encoding UTF8
try {
    $desktop = [Environment]::GetFolderPath('Desktop')
    Copy-Item $ReportPath (Join-Path $desktop 'GLASSCORE_BUILD_REPORT.txt') -Force
} catch { }

# ── 4) shutdown only after everything is done ─────────────────────────────────────────────
if ($NoShutdown) {
    Log 'NoShutdown set - leaving the PC on.'
    exit ($(if ($gameOk) { 0 } else { 1 }))
}
if ($ShutdownOnlyOnSuccess -and -not $gameOk) {
    Log 'Build failed and -ShutdownOnlyOnSuccess is set - leaving the PC on.'
    exit 1
}
$comment = "GLASSCORE build finished ($(if ($gameOk) { 'success' } else { 'failed' })). Report on your Desktop. Cancel with: shutdown /a"
Log "Shutting down in $ShutdownDelaySeconds seconds. Cancel with:  shutdown /a"
$script:Lines | Set-Content -Path $ReportPath -Encoding UTF8
& shutdown.exe /s /t $ShutdownDelaySeconds /c $comment
exit ($(if ($gameOk) { 0 } else { 1 }))
