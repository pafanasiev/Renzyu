using System.Text.Json;

namespace Host.Models
{
    public interface IGameTelemetry
    {
        void RecordMove(int x, int y, int mark, bool won);
        void MarkComputerLost();
    }

    public sealed class FileGameTelemetry : IGameTelemetry
    {
        public const string DirectoryEnvironmentVariable = "RENZYU_GAME_TELEMETRY_DIRECTORY";

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly object sync = new object();
        private readonly Guid gameId;
        private string filePath;
        private int nextTurn = 1;
        private bool computerLost;

        public FileGameTelemetry(Guid gameId, string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("A telemetry directory is required.", nameof(directory));

            this.gameId = gameId;
            Directory.CreateDirectory(directory);
            filePath = Path.Combine(directory, $"game-{gameId:N}.jsonl");

            using var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        }

        public static FileGameTelemetry CreateDefault(Guid gameId)
        {
            string directory = Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(directory))
                directory = Path.Combine(AppContext.BaseDirectory, "telemetry");

            return new FileGameTelemetry(gameId, directory);
        }

        public void RecordMove(int x, int y, int mark, bool won)
        {
            if (mark != ComputerGame.PLAYER_MARK && mark != ComputerGame.COMPUTER_MARK)
                throw new ArgumentOutOfRangeException(nameof(mark), "The move mark must identify a player.");

            lock (sync)
            {
                if (computerLost)
                    throw new InvalidOperationException("Cannot append moves after the computer has lost.");

                var move = new GameMoveTelemetry
                {
                    SchemaVersion = 1,
                    GameId = gameId,
                    Turn = nextTurn,
                    X = x,
                    Y = y,
                    Mark = mark,
                    Actor = mark == ComputerGame.COMPUTER_MARK ? "computer" : "human",
                    Won = won,
                    OccurredAtUtc = DateTimeOffset.UtcNow
                };
                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(move, SerializerOptions);

                using var stream = new FileStream(
                    filePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    FileOptions.WriteThrough);
                stream.Write(payload, 0, payload.Length);
                stream.WriteByte((byte)'\n');
                stream.Flush(flushToDisk: true);
                nextTurn++;
            }
        }

        public void MarkComputerLost()
        {
            lock (sync)
            {
                if (computerLost)
                    return;

                string lostPath = Path.Combine(
                    Path.GetDirectoryName(filePath)!,
                    $"{Path.GetFileNameWithoutExtension(filePath)}.computer-lost.jsonl");
                File.Move(filePath, lostPath);
                filePath = lostPath;
                computerLost = true;
            }
        }

        private sealed class GameMoveTelemetry
        {
            public int SchemaVersion { get; set; }
            public Guid GameId { get; set; }
            public int Turn { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Mark { get; set; }
            public string Actor { get; set; }
            public bool Won { get; set; }
            public DateTimeOffset OccurredAtUtc { get; set; }
        }
    }
}
