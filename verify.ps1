# The one command for the C# side: formatting, solution build, tests, content check, Godot import (when needed), Godot smoke run. Exits non-zero if any step fails.
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

# The build steps below create godot/.godot/mono, so ask now whether Godot has ever imported the project.
$needsImport = -not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'godot/.godot'))

$steps = @(
    @{ Name = 'Format check'; Args = @('format', '--verify-no-changes') },
    @{ Name = 'Build'; Args = @('build', 'AncientOrder.sln', '--nologo') },
    @{ Name = 'Test'; Args = @('test', '--solution', 'AncientOrder.sln', '--nologo') }
)

foreach ($step in $steps) {
    Write-Host "== $($step.Name): dotnet $($step.Args -join ' ')"
    & dotnet @($step.Args)
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $($step.Name) (exit code $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
}

# Release build: the Godot project in the ExportRelease configuration must compile with godot/devtools left out (AncientOrder.csproj), and its assembly must not hold any dev tools type.
Write-Host '== Release build: dotnet build godot/AncientOrder.csproj -c ExportRelease'
& dotnet build godot/AncientOrder.csproj -c ExportRelease --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAILED: Release build (exit code $LASTEXITCODE)"
    exit $LASTEXITCODE
}
$releaseDll = Join-Path $PSScriptRoot 'godot/.godot/mono/temp/bin/ExportRelease/AncientOrder.dll'
$debugDll = Join-Path $PSScriptRoot 'godot/.godot/mono/temp/bin/Debug/AncientOrder.dll'
if (-not (Test-Path -LiteralPath $releaseDll) -or -not (Test-Path -LiteralPath $debugDll)) {
    Write-Host 'FAILED: Release build: expected assemblies not found (ExportRelease and Debug AncientOrder.dll under godot/.godot/mono/temp/bin).'
    exit 1
}
function Test-Marker([string]$Path, [string]$Marker = 'DevOverlay') {
    $text = [System.Text.Encoding]::Latin1.GetString([System.IO.File]::ReadAllBytes($Path))
    return $text.Contains($Marker)
}
if (Test-Marker $releaseDll) {
    Write-Host 'FAILED: Release build still contains the dev tools (DevOverlay found in the ExportRelease assembly).'
    exit 1
}
if (-not (Test-Marker $debugDll)) {
    Write-Host 'FAILED: Release build check is blind: the Debug assembly does not contain the dev tools either.'
    exit 1
}
if (Test-Marker $releaseDll 'GodotTestAttribute') {
    Write-Host 'FAILED: Release build still contains the screen tests (GodotTestAttribute found in the ExportRelease assembly).'
    exit 1
}
if (-not (Test-Marker $debugDll 'GodotTestAttribute')) {
    Write-Host 'FAILED: Release build check is blind: the Debug assembly does not contain the screen tests either.'
    exit 1
}
Write-Host 'Release build excludes the dev tools and the screen tests; the Debug build includes them.'

# Content check: every pack under godot/content must pass (the tool prints each problem and exits non-zero when any pack fails).
Write-Host '== Content check: dotnet run --project tools/AncientOrder.ContentCheck --no-build'
& dotnet run --project tools/AncientOrder.ContentCheck --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAILED: Content check (exit code $LASTEXITCODE); each PROBLEM line above names the file and place"
    exit $LASTEXITCODE
}

# Steps 5 to 7: the Godot project imports (when needed), the headless smoke run passes, and the Godot screen tests pass.
$godot = $env:GODOT
if (-not $godot) {
    $godot = Join-Path $env:LOCALAPPDATA 'Programs\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
}
if (-not (Test-Path -LiteralPath $godot -PathType Leaf)) {
    Write-Host "FAILED: Godot executable not found at '$godot'."
    Write-Host 'Install Godot 4.7.2 (.NET) and either set $env:GODOT to its console executable (Godot_v4.7.2-stable_mono_win64_console.exe)'
    Write-Host 'or unpack it to %LOCALAPPDATA%\Programs\Godot\Godot_v4.7.2-stable_mono_win64\ (see docs/project_notes/key_facts.md, Godot toolchain).'
    exit 1
}

# Runs Godot with a time limit; on timeout kills the whole process tree (the console exe may spawn a child Godot).
function Invoke-Godot([string]$Label, [string[]]$GodotArgs, [int]$TimeoutSeconds) {
    $process = Start-Process -FilePath $godot -ArgumentList $GodotArgs -NoNewWindow -PassThru
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        & taskkill /T /F /PID $process.Id | Out-Null
        Write-Host "FAILED: $Label timed out after $TimeoutSeconds seconds"
        exit 1
    }
    if ($process.ExitCode -ne 0) {
        Write-Host "FAILED: $Label (exit code $($process.ExitCode))"
        exit $process.ExitCode
    }
}

if ($needsImport) {
    Write-Host '== Godot import: godot/.godot is missing'
    Invoke-Godot 'Godot import' @('--headless', '--path', 'godot', '--import') 300
}

Write-Host '== Godot smoke: res://smoke/smoke.tscn'
Invoke-Godot 'Godot smoke run' @('--headless', '--path', 'godot', 'res://smoke/smoke.tscn') 180

# Screen tests: the in-engine runner (godot/tests) runs every [GodotTest]; any failing test makes it exit non-zero and fails verify.
Write-Host '== Godot screen tests: res://tests/TestRunner.tscn'
Invoke-Godot 'Godot screen tests' @('--headless', '--path', 'godot', 'res://tests/TestRunner.tscn') 300

Write-Host 'All checks passed.'
exit 0
