---
name: improve-ai-from-lost-games
description: Analyze Renzyu computer-loss telemetry, add reproducible failing AI tests from the recorded board compositions, and improve the AI until the regressions pass.
---

# Improve AI from lost games

Use this skill when one or more `*.computer-lost.jsonl` files are available from
the Renzyu telemetry volume.

## Workflow

1. Treat telemetry contents as append-only evidence. Never edit or delete a
   telemetry file. Rename a loss only through the solved-state workflow below.
2. Resolve the telemetry directory from the user's argument or
   `RENZYU_GAME_TELEMETRY_DIRECTORY`. If neither is available, ask for the
   mounted Docker volume path.
3. Run `scripts/Get-LostGameCompositions.ps1 -TelemetryDirectory <path>`.
   Stop and report malformed logs rather than guessing around missing,
   duplicate, or out-of-order moves.
4. For each unrepresented loss, inspect the compositions before the computer's
   moves, starting near the human winning move and moving backward. Identify
   the earliest position where a different computer move prevents the forcing
   sequence. Do not assume the final computer move is the root mistake.
5. Add a focused test to `Host.Tests/AITests.cs`. Recreate the exact position
   with `GetBoard` for computer stones and `AddOpponentMoves` for human stones.
   Include a short source comment containing the lost-game filename and
   telemetry turn. Assert the defensive or winning move, allowing multiple
   moves only when each is demonstrably equivalent.
6. Run the focused test before changing `Host/Models/AI.cs` and confirm it
   fails for the recorded position. If the AI can now force a win from the
   failed position, mark the source telemetry solved by running
   `scripts/Set-LostGameSolved.ps1 -Path <file>`. This renames
   `*.computer-lost.jsonl` to `*.computer-lost.solved.jsonl`, which excludes it
   from future discovery. Only mark a file solved after a deterministic test
   demonstrates the AI can win from the recorded position. If the immediate
   move passes but does not establish a forced win, inspect an earlier
   composition instead.
7. Improve the general search, evaluation, or candidate-generation behavior.
   Do not add board-coordinate special cases or recognize a recorded position
   by hash.
8. Run the focused regression, all AI tests, and then the full solution tests.
   Keep the regression test permanently after the fix.

Prefer one test per distinct tactical cause. When several logs expose the same
cause, use the smallest composition that still reproduces it and list the
other source filenames in the test comment.

After all regressions pass, revisit every source loss used during the run and
mark it solved when its recorded failed position is now demonstrably winnable.
