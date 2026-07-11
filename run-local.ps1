[CmdletBinding()]
param(
    [ValidateRange(1, 65535)]
    [int]$Port = 51234,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $repositoryRoot "Host\Host.csproj"
$url = "http://localhost:$Port"

$activePorts = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port
if ($activePorts -contains $Port) {
    throw "Port $Port is already in use."
}

Write-Host "Renzyu will run at $url"
Write-Host "Press Ctrl+C to stop Kestrel."

& dotnet run --project $projectPath --configuration $Configuration -- --urls $url
if ($LASTEXITCODE -ne 0) {
    throw "Renzyu exited with code $LASTEXITCODE."
}
