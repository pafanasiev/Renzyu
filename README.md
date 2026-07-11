# Renzyu

Renzyu is an ASP.NET Core MVC and SignalR application targeting .NET 8 LTS.

## Local development

Install the .NET 8 SDK, Node.js 24 LTS, and npm. The application build restores
the locked browser dependencies and generates the assets under
`Host\wwwroot\Scripts\vendor`.

```powershell
dotnet restore Renzyu.sln --locked-mode
dotnet build Renzyu.sln --no-restore
dotnet test Renzyu.sln --no-build
.\run-local.ps1
```

Pass `-Port` or `-Configuration` to `run-local.ps1` when needed.

## Train a reinforcement-learning AI

Run the trainer from the repository root. It combines reward-gated policy
distillation from a deeper minimax explorer with epsilon-greedy
temporal-difference learning. The distilled policy handles successful benchmark
trajectories; a TD-updated linear policy handles positions outside them.
Versioned JSON models are saved under `Host\TrainedModels`.

```powershell
dotnet run --project Trainer -- --name "My first agent" --episodes 500
```

By default, the trainer benchmarks both playing orders against the same depth-4,
60,000-node minimax configuration used by the browser. It stops early only after
scoring at least 75%, returns a non-zero exit code if that target is missed, and
enforces a five-minute maximum. The best evaluated policy is saved rather than
the last policy.

The default score is deterministic replay against the training opponent. It
verifies the saved policy but does not measure strength on unseen human openings;
see `docs\reinforcement-learning-pipeline.md` for the design and limitations.

The live terminal arena shows the current board, training and evaluation W/D/L,
distilled policy size, TD error, exploration rate, games per second, ETA, an
evaluation trend, and the strongest learned feature weights. Checkpoints are
saved every 100 episodes by default. Press Ctrl+C to stop after the current game
and preserve the best model. Run `dotnet run --project Trainer -- --help` for all
tuning options.

Start the web app after training (or refresh an open game page), choose the
saved model from **Choose your AI opponent**, and select **Play selected AI**.
Model JSON files are intentionally ignored by Git; copy or mount the files when
moving trained opponents between machines.

## Container

Build and run the Linux image with Docker:

```powershell
docker build --tag renzyu .
docker volume create renzyu-telemetry
docker volume create renzyu-models
docker run --rm --publish 8080:8080 `
  --mount type=volume,source=renzyu-telemetry,target=/data/telemetry `
  --mount type=volume,source=renzyu-models,target=/data/models `
  renzyu
```

Open `http://localhost:8080`. The final image uses the official non-root
ASP.NET Core 8 runtime image; the .NET SDK and Node.js remain in build stages.

Computer games write one append-only JSONL file per game to `/data/telemetry`.
Games won by a human are renamed with a `.computer-lost.jsonl` suffix. Set
`RENZYU_GAME_TELEMETRY_DIRECTORY` to override the path outside Docker.
Trained opponents are loaded from `/data/models`; set
`RENZYU_AI_MODEL_DIRECTORY` to override that path.

NuGet and npm dependencies are locked. Use locked restore mode in CI so
dependency graph changes must be committed explicitly.
