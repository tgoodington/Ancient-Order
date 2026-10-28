# The one command for the C# side: formatting, solution build, tests, Godot import (when needed), Godot smoke run. Exits non-zero if any step fails.
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

# Steps 4 and 5: the Godot project imports (when needed) and the headless smoke run passes.
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

Write-Host 'All checks passed.'
exit 0
