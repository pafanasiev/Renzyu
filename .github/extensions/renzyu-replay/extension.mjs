import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { basename, resolve } from "node:path";
import {
    CanvasError,
    createCanvas,
    joinSession,
} from "@github/copilot-sdk/extension";

const servers = new Map();

async function loadReplay(filePath) {
    if (typeof filePath !== "string" || filePath.trim() === "") {
        throw new CanvasError("replay_path_required", "A telemetry file path is required.");
    }

    const resolvedPath = resolve(filePath);
    let contents;
    try {
        contents = await readFile(resolvedPath, "utf8");
    } catch (error) {
        if (error?.code === "ENOENT") {
            throw new CanvasError("replay_not_found", `Replay file not found: ${resolvedPath}`);
        }
        throw error;
    }

    const lines = contents
        .split(/\r?\n/)
        .map((line) => line.trim())
        .filter(Boolean);
    if (lines.length === 0) {
        throw new CanvasError("replay_empty", "The replay file contains no moves.");
    }

    const moves = [];
    const occupied = new Set();
    let gameId;
    for (let index = 0; index < lines.length; index++) {
        const expectedTurn = index + 1;
        let move;
        try {
            move = JSON.parse(lines[index]);
        } catch {
            throw new CanvasError(
                "replay_invalid_json",
                `Move ${expectedTurn} is not valid JSON.`,
            );
        }

        const expectedMark = expectedTurn % 2 === 1 ? 1 : 2;
        if (move.schemaVersion !== 1
            || move.turn !== expectedTurn
            || move.mark !== expectedMark
            || !Number.isInteger(move.x)
            || !Number.isInteger(move.y)
            || move.x < 0
            || move.x >= 19
            || move.y < 0
            || move.y >= 19
            || Number.isNaN(Date.parse(move.occurredAtUtc))) {
            throw new CanvasError(
                "replay_invalid_move",
                `Move ${expectedTurn} has invalid schema, order, mark, coordinates, or timestamp.`,
            );
        }

        if (index === 0) {
            gameId = move.gameId;
        } else if (move.gameId !== gameId) {
            throw new CanvasError(
                "replay_mixed_games",
                `Move ${expectedTurn} belongs to another game.`,
            );
        }

        const expectedActor = move.mark === 1 ? "human" : "computer";
        if (move.actor !== expectedActor) {
            throw new CanvasError(
                "replay_invalid_actor",
                `Move ${expectedTurn} has an actor that does not match its mark.`,
            );
        }

        const cellKey = `${move.x}:${move.y}`;
        if (occupied.has(cellKey)) {
            throw new CanvasError(
                "replay_duplicate_cell",
                `Move ${expectedTurn} reuses occupied cell ${cellKey}.`,
            );
        }
        occupied.add(cellKey);

        if (move.won && index !== lines.length - 1) {
            throw new CanvasError(
                "replay_moves_after_win",
                `Move ${expectedTurn} wins, but later moves are present.`,
            );
        }
        moves.push(move);
    }

    return {
        fileName: basename(resolvedPath),
        filePath: resolvedPath,
        gameId,
        moves,
        winner: moves.at(-1).won ? moves.at(-1).actor : null,
    };
}

function escapeHtml(value) {
    return String(value)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}

function renderHtml(replay) {
    const serializedReplay = JSON.stringify(replay).replaceAll("<", "\\u003c");
    const resultLabel = replay.winner
        ? `${escapeHtml(replay.winner)} won`
        : "unfinished";

    return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Renzyu game replay</title>
  <style>
    :root { color-scheme: light dark; }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      background: var(--background-color-default, #f6f8fa);
      color: var(--text-color-default, #1f2328);
      font-family: var(--font-sans, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif);
      font-size: var(--text-body-medium, 14px);
      line-height: var(--leading-body-medium, 20px);
    }
    main {
      display: grid;
      gap: 16px;
      padding: 16px;
    }
    header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
    }
    h1 {
      margin: 0 0 4px;
      font-size: var(--text-title-medium, 20px);
      line-height: 1.25;
    }
    .subtle {
      color: var(--text-color-muted, #59636e);
      font-size: 12px;
      overflow-wrap: anywhere;
    }
    .badge {
      border: 1px solid var(--true-color-red, #cf222e);
      border-radius: 999px;
      color: var(--true-color-red, #cf222e);
      flex: none;
      font-weight: var(--font-weight-semibold, 600);
      padding: 3px 9px;
    }
    .panel {
      background: var(--background-color-default, #fff);
      border: 1px solid var(--border-color-default, #d0d7de);
      border-radius: 10px;
      box-shadow: 0 1px 2px rgba(31, 35, 40, 0.08);
      overflow: hidden;
    }
    .board-shell { padding: 10px; }
    canvas {
      display: block;
      height: auto;
      max-width: 100%;
      width: 100%;
    }
    .controls {
      display: grid;
      gap: 10px;
      padding: 12px;
    }
    .button-row {
      align-items: center;
      display: flex;
      gap: 8px;
    }
    button, select {
      background: var(--background-color-default, #fff);
      border: 1px solid var(--border-color-default, #d0d7de);
      border-radius: 6px;
      color: var(--text-color-default, #1f2328);
      font: inherit;
      min-height: 32px;
      padding: 5px 10px;
    }
    button:hover { background: var(--background-color-muted, #f3f4f6); }
    button:focus-visible, select:focus-visible, input:focus-visible {
      outline: 2px solid var(--color-focus-outline, #0969da);
      outline-offset: 2px;
    }
    #play { min-width: 76px; }
    #turn {
      accent-color: var(--true-color-blue, #0969da);
      width: 100%;
    }
    .status {
      align-items: center;
      display: flex;
      justify-content: space-between;
      gap: 10px;
    }
    .legend {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      color: var(--text-color-muted, #59636e);
      font-size: 12px;
    }
    .legend-item { align-items: center; display: inline-flex; gap: 5px; }
    .dot { border-radius: 50%; display: inline-block; height: 10px; width: 10px; }
    .human { background: var(--true-color-blue, #2563eb); }
    .computer { background: var(--true-color-orange, #ea580c); }
    .last { background: var(--true-color-yellow, #ca8a04); }
    .moves {
      max-height: 240px;
      overflow: auto;
    }
    .move {
      align-items: center;
      background: transparent;
      border: 0;
      border-bottom: 1px solid var(--border-color-default, #d8dee4);
      border-radius: 0;
      display: grid;
      grid-template-columns: 38px 1fr auto;
      text-align: left;
      width: 100%;
    }
    .move:last-child { border-bottom: 0; }
    .move.active {
      background: color-mix(in srgb, var(--true-color-blue, #0969da) 12%, transparent);
      font-weight: var(--font-weight-semibold, 600);
    }
    .move.winner { color: var(--true-color-red, #cf222e); }
    code {
      font-family: var(--font-mono, Consolas, monospace);
      font-size: var(--text-code-inline, 12px);
    }
    @media (min-width: 760px) {
      main { grid-template-columns: minmax(360px, 1fr) minmax(240px, 320px); }
      header { grid-column: 1 / -1; }
      .right { display: grid; align-content: start; gap: 16px; }
    }
  </style>
</head>
<body>
  <main>
    <header>
      <div>
        <h1>Renzyu loss replay</h1>
        <div class="subtle"><code>${escapeHtml(replay.fileName)}</code></div>
        <div class="subtle">Game ${escapeHtml(replay.gameId)}</div>
      </div>
      <span class="badge">${resultLabel}</span>
    </header>

    <section class="panel board-shell">
      <canvas id="board" aria-label="19 by 19 Renzyu replay board"></canvas>
    </section>

    <div class="right">
      <section class="panel controls">
        <div class="status">
          <strong id="turn-label">Opening position</strong>
          <span id="coordinate" class="subtle">0 / ${replay.moves.length}</span>
        </div>
        <input id="turn" type="range" min="0" max="${replay.moves.length}" value="${replay.moves.length}" />
        <div class="button-row">
          <button id="previous" type="button" aria-label="Previous move">&#9664;</button>
          <button id="play" type="button">Play</button>
          <button id="next" type="button" aria-label="Next move">&#9654;</button>
          <select id="speed" aria-label="Playback speed">
            <option value="1000">1x</option>
            <option value="500" selected>2x</option>
            <option value="250">4x</option>
          </select>
        </div>
        <div class="legend">
          <span class="legend-item"><i class="dot human"></i>Human</span>
          <span class="legend-item"><i class="dot computer"></i>Trained AI</span>
          <span class="legend-item"><i class="dot last"></i>Last move</span>
        </div>
      </section>

      <section class="panel moves" id="moves" aria-label="Move list"></section>
    </div>
  </main>
  <script>
    const replay = ${serializedReplay};
    const canvas = document.getElementById("board");
    const slider = document.getElementById("turn");
    const turnLabel = document.getElementById("turn-label");
    const coordinate = document.getElementById("coordinate");
    const playButton = document.getElementById("play");
    const speed = document.getElementById("speed");
    const moveList = document.getElementById("moves");
    let currentTurn = replay.moves.length;
    let timer = null;

    function theme(name, fallback) {
      const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
      return value || fallback;
    }

    function appliedBoard() {
      const board = Array.from({ length: 19 }, () => Array(19).fill(0));
      for (const move of replay.moves.slice(0, currentTurn)) board[move.x][move.y] = move.mark;
      return board;
    }

    function winningCells(board, move) {
      if (!move || !move.won) return new Set();
      const directions = [[1, 0], [0, 1], [1, 1], [1, -1]];
      for (const direction of directions) {
        const dx = direction[0];
        const dy = direction[1];
        const cells = [[move.x, move.y]];
        for (const sign of [-1, 1]) {
          let x = move.x + dx * sign;
          let y = move.y + dy * sign;
          while (x >= 0 && x < 19 && y >= 0 && y < 19 && board[x][y] === move.mark) {
            cells.push([x, y]);
            x += dx * sign;
            y += dy * sign;
          }
        }
        if (cells.length >= 5) {
          return new Set(cells.map((cell) => cell[0] + ":" + cell[1]));
        }
      }
      return new Set();
    }

    function draw() {
      const cssSize = Math.max(320, Math.min(680, canvas.parentElement.clientWidth - 20));
      const scale = window.devicePixelRatio || 1;
      canvas.width = Math.floor(cssSize * scale);
      canvas.height = Math.floor(cssSize * scale);
      canvas.style.width = cssSize + "px";
      canvas.style.height = cssSize + "px";
      const ctx = canvas.getContext("2d");
      ctx.scale(scale, scale);

      const margin = 24;
      const step = (cssSize - margin * 2) / 18;
      ctx.fillStyle = theme("--background-color-muted", "#e8d2a8");
      ctx.fillRect(0, 0, cssSize, cssSize);
      ctx.strokeStyle = theme("--text-color-muted", "#6b5437");
      ctx.lineWidth = 1;
      for (let index = 0; index < 19; index++) {
        const position = margin + index * step;
        ctx.beginPath();
        ctx.moveTo(margin, position);
        ctx.lineTo(cssSize - margin, position);
        ctx.moveTo(position, margin);
        ctx.lineTo(position, cssSize - margin);
        ctx.stroke();
      }

      ctx.fillStyle = theme("--text-color-muted", "#59636e");
      ctx.font = "10px " + theme("--font-mono", "monospace");
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      for (let index = 0; index < 19; index += 3) {
        const position = margin + index * step;
        ctx.fillText(String(index), position, 10);
        ctx.fillText(String(index), 10, position);
      }

      const board = appliedBoard();
      const lastMove = currentTurn > 0 ? replay.moves[currentTurn - 1] : null;
      const winning = winningCells(board, lastMove);
      for (let turn = 0; turn < currentTurn; turn++) {
        const move = replay.moves[turn];
        const x = margin + move.x * step;
        const y = margin + move.y * step;
        ctx.beginPath();
        ctx.arc(x, y, Math.max(5, step * 0.37), 0, Math.PI * 2);
        ctx.fillStyle = move.mark === 1
          ? theme("--true-color-blue", "#2563eb")
          : theme("--true-color-orange", "#ea580c");
        ctx.fill();
        if (winning.has(move.x + ":" + move.y)) {
          ctx.strokeStyle = theme("--true-color-red", "#dc2626");
          ctx.lineWidth = 4;
          ctx.stroke();
        } else if (turn === currentTurn - 1) {
          ctx.strokeStyle = theme("--true-color-yellow", "#facc15");
          ctx.lineWidth = 3;
          ctx.stroke();
        }
        ctx.fillStyle = "#ffffff";
        ctx.font = "bold " + Math.max(8, step * 0.33) + "px "
          + theme("--font-sans", "sans-serif");
        ctx.fillText(String(move.turn), x, y);
      }
    }

    function update() {
      slider.value = String(currentTurn);
      const move = currentTurn > 0 ? replay.moves[currentTurn - 1] : null;
      if (move) {
        turnLabel.textContent = "Turn " + move.turn + " - " + move.actor
          + (move.won ? " wins" : "");
        coordinate.textContent = "(" + move.x + ", " + move.y + ")  "
          + currentTurn + " / " + replay.moves.length;
      } else {
        turnLabel.textContent = "Opening position";
        coordinate.textContent = "0 / " + replay.moves.length;
      }
      for (const element of moveList.children) {
        element.classList.toggle("active", Number(element.dataset.turn) === currentTurn);
      }
      draw();
    }

    function stop() {
      if (timer !== null) window.clearInterval(timer);
      timer = null;
      playButton.textContent = "Play";
    }

    function play() {
      if (timer !== null) {
        stop();
        return;
      }
      if (currentTurn >= replay.moves.length) currentTurn = 0;
      playButton.textContent = "Pause";
      timer = window.setInterval(() => {
        currentTurn += 1;
        update();
        if (currentTurn >= replay.moves.length) stop();
      }, Number(speed.value));
    }

    for (const move of replay.moves) {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "move" + (move.won ? " winner" : "");
      button.dataset.turn = String(move.turn);
      button.innerHTML = "<span>#" + move.turn + "</span><span>"
        + (move.actor === "human" ? "Human" : "Trained AI")
        + "</span><code>(" + move.x + ", " + move.y + ")</code>";
      button.addEventListener("click", () => {
        stop();
        currentTurn = move.turn;
        update();
      });
      moveList.appendChild(button);
    }

    slider.addEventListener("input", () => {
      stop();
      currentTurn = Number(slider.value);
      update();
    });
    document.getElementById("previous").addEventListener("click", () => {
      stop();
      currentTurn = Math.max(0, currentTurn - 1);
      update();
    });
    document.getElementById("next").addEventListener("click", () => {
      stop();
      currentTurn = Math.min(replay.moves.length, currentTurn + 1);
      update();
    });
    playButton.addEventListener("click", play);
    window.addEventListener("resize", draw);
    window.addEventListener("keydown", (event) => {
      if (event.key === "ArrowLeft") document.getElementById("previous").click();
      if (event.key === "ArrowRight") document.getElementById("next").click();
      if (event.key === " ") {
        event.preventDefault();
        play();
      }
    });
    update();
  </script>
</body>
</html>`;
}

async function startServer(replay) {
    const state = { replay };
    const server = createServer((req, res) => {
        if (req.url !== "/") {
            res.statusCode = 404;
            res.end("Not found");
            return;
        }
        res.setHeader("Cache-Control", "no-store");
        res.setHeader("Content-Type", "text/html; charset=utf-8");
        res.end(renderHtml(state.replay));
    });
    await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
    const address = server.address();
    const port = typeof address === "object" && address ? address.port : 0;
    return { server, state, url: `http://127.0.0.1:${port}/` };
}

await joinSession({
    canvases: [
        createCanvas({
            id: "renzyu-replay",
            displayName: "Renzyu replay",
            description: "Interactively replay a validated Renzyu JSONL game.",
            inputSchema: {
                type: "object",
                additionalProperties: false,
                required: ["filePath"],
                properties: {
                    filePath: {
                        type: "string",
                        minLength: 1,
                    },
                },
            },
            actions: [
                {
                    name: "get_summary",
                    description: "Return the game result and move count for the open replay.",
                    handler: (ctx) => {
                        const entry = servers.get(ctx.instanceId);
                        if (!entry) {
                            throw new CanvasError("replay_not_open", "The replay is not open.");
                        }
                        return {
                            fileName: entry.state.replay.fileName,
                            gameId: entry.state.replay.gameId,
                            moves: entry.state.replay.moves.length,
                            winner: entry.state.replay.winner,
                        };
                    },
                },
            ],
            open: async (ctx) => {
                const replay = await loadReplay(ctx.input.filePath);
                let entry = servers.get(ctx.instanceId);
                if (!entry) {
                    entry = await startServer(replay);
                    servers.set(ctx.instanceId, entry);
                } else {
                    entry.state.replay = replay;
                }
                return {
                    title: `Replay: ${replay.fileName}`,
                    status: `${replay.moves.length} moves - ${replay.winner ?? "unfinished"}`,
                    url: entry.url,
                };
            },
            onClose: async (ctx) => {
                const entry = servers.get(ctx.instanceId);
                if (entry) {
                    servers.delete(ctx.instanceId);
                    await new Promise((resolve) => entry.server.close(() => resolve()));
                }
            },
        }),
    ],
});
