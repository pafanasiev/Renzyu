[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TelemetryDirectory
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $TelemetryDirectory -PathType Container)) {
    throw "Telemetry directory does not exist: $TelemetryDirectory"
}

$files = @(Get-ChildItem -LiteralPath $TelemetryDirectory -Filter "*.computer-lost.jsonl" -File -Recurse)
if ($files.Count -eq 0) {
    throw "No computer-loss telemetry files were found under $TelemetryDirectory"
}

$games = foreach ($file in $files) {
    $moves = @()
    $positions = @()
    $occupied = @{}
    $expectedTurn = 1
    $gameId = $null

    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $move = $line | ConvertFrom-Json
        if ($move.schemaVersion -ne 1) {
            throw "Unsupported schema version in $($file.FullName): $($move.schemaVersion)"
        }
        if ($move.turn -ne $expectedTurn) {
            throw "Expected turn $expectedTurn in $($file.FullName), found $($move.turn)"
        }
        if ($move.mark -ne 1 -and $move.mark -ne 2) {
            throw "Invalid mark at turn $expectedTurn in $($file.FullName): $($move.mark)"
        }
        $expectedMark = if ($expectedTurn % 2 -eq 1) { 1 } else { 2 }
        if ($move.mark -ne $expectedMark) {
            throw "Expected mark $expectedMark at turn $expectedTurn in $($file.FullName)"
        }
        $expectedActor = if ($move.mark -eq 1) { "human" } else { "computer" }
        if ($move.actor -ne $expectedActor) {
            throw "Expected actor $expectedActor at turn $expectedTurn in $($file.FullName)"
        }
        if ($move.x -lt 0 -or $move.x -ge 19 -or $move.y -lt 0 -or $move.y -ge 19) {
            throw "Out-of-range move at turn $expectedTurn in $($file.FullName)"
        }

        $coordinate = "$($move.x):$($move.y)"
        if ($occupied.ContainsKey($coordinate)) {
            throw "Duplicate move at $coordinate in $($file.FullName)"
        }

        if ($null -eq $gameId) {
            $gameId = $move.gameId
        }
        elseif ($gameId -ne $move.gameId) {
            throw "Multiple game IDs found in $($file.FullName)"
        }

        if ($move.mark -eq 2) {
            $positions += [ordered]@{
                turn = $move.turn
                actualMove = [ordered]@{ x = $move.x; y = $move.y }
                computerMoves = @($moves | Where-Object mark -eq 2 | ForEach-Object {
                    [ordered]@{ x = $_.x; y = $_.y }
                })
                playerMoves = @($moves | Where-Object mark -eq 1 | ForEach-Object {
                    [ordered]@{ x = $_.x; y = $_.y }
                })
            }
        }

        $moves += $move
        $occupied[$coordinate] = $true
        $expectedTurn++
    }

    if ($moves.Count -eq 0) {
        throw "Lost-game telemetry is empty: $($file.FullName)"
    }

    $winningMove = $moves[$moves.Count - 1]
    if ($winningMove.mark -ne 1 -or -not $winningMove.won) {
        throw "Lost-game telemetry does not end in a human winning move: $($file.FullName)"
    }
    if (@($moves | Select-Object -SkipLast 1 | Where-Object won).Count -ne 0) {
        throw "Lost-game telemetry contains a winning move before its final turn: $($file.FullName)"
    }

    [ordered]@{
        file = $file.FullName
        gameId = $gameId
        winningMove = [ordered]@{ turn = $winningMove.turn; x = $winningMove.x; y = $winningMove.y }
        positionsBeforeComputerMoves = $positions
    }
}

$games | ConvertTo-Json -Depth 10
