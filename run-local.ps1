[CmdletBinding()]
param(
    [ValidateRange(1, 65535)]
    [int]$Port = 51234,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

function Get-ProgramFilesRoots {
    return @($env:ProgramFiles, ${env:ProgramFiles(x86)}) |
        Where-Object { $_ } |
        Select-Object -Unique
}

function Find-MSBuild {
    $command = Get-Command "MSBuild.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    foreach ($root in Get-ProgramFilesRoots) {
        $vsWhere = Join-Path $root "Microsoft Visual Studio\Installer\vswhere.exe"
        if (Test-Path $vsWhere) {
            $path = & $vsWhere -latest -products * -requires Microsoft.Component.MSBuild `
                -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
            if ($path) {
                return $path
            }
        }
    }

    throw "MSBuild was not found. Install Visual Studio or Visual Studio Build Tools with the .NET desktop build tools workload."
}

function Find-IISExpress {
    foreach ($root in Get-ProgramFilesRoots) {
        $candidate = Join-Path $root "IIS Express\iisexpress.exe"
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "IIS Express was not found. Install IIS Express or Visual Studio with ASP.NET and web development tools."
}

$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $repositoryRoot "Host\Host.csproj"
$webRoot = Join-Path $repositoryRoot "Host"
$msBuild = Find-MSBuild
$iisExpress = Find-IISExpress

Write-Host "Building Renzyu ($Configuration)..."
& $msBuild $projectPath /t:Build "/p:Configuration=$Configuration" /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

$activePorts = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port
if ($activePorts -contains $Port) {
    throw "Port $Port is already in use."
}

$url = "http://localhost:$Port/"
Write-Host "Renzyu is running at $url"
Write-Host "Press Ctrl+C to stop IIS Express."

& $iisExpress "/path:$webRoot" "/port:$Port" /systray:false
if ($LASTEXITCODE -ne 0) {
    throw "IIS Express exited with code $LASTEXITCODE."
}
