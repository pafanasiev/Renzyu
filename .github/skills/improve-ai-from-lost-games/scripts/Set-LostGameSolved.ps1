[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

$ErrorActionPreference = "Stop"

$file = Get-Item -LiteralPath $Path -ErrorAction Stop
if ($file.PSIsContainer) {
    throw "Lost-game telemetry path must be a file: $Path"
}
if ($file.Name -notlike "*.computer-lost.jsonl") {
    throw "Expected a filename ending in .computer-lost.jsonl: $($file.Name)"
}

$solvedName = $file.Name.Substring(0, $file.Name.Length - ".jsonl".Length) + ".solved.jsonl"
$solvedPath = Join-Path $file.DirectoryName $solvedName
if (Test-Path -LiteralPath $solvedPath) {
    throw "Solved telemetry already exists: $solvedPath"
}

if ($PSCmdlet.ShouldProcess($file.FullName, "Mark lost game solved")) {
    Move-Item -LiteralPath $file.FullName -Destination $solvedPath
    Get-Item -LiteralPath $solvedPath
}
