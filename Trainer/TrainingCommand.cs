namespace Renzyu.Training
{
    internal static class TrainingCommand
    {
        public static int Run(string[] args)
        {
            TrainingOptions options;
            try
            {
                options = TrainingOptions.Parse(args);
            }
            catch (ArgumentException exception)
            {
                Console.Error.WriteLine("error: " + exception.Message);
                Console.Error.WriteLine();
                WriteHelp(Console.Error);
                return 2;
            }

            if (options.ShowHelp)
            {
                WriteHelp(Console.Out);
                return 0;
            }

            using var cancellation = new CancellationTokenSource(
                TimeSpan.FromSeconds(options.TimeLimitSeconds));
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellation.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;

            try
            {
                using var dashboard = new TrainingDashboard(options.Name, options.Dashboard);
                var trainer = new ReinforcementTrainer(options);
                TrainingResult result = trainer.Train(cancellation.Token, dashboard.Render);
                dashboard.Dispose();

                Console.WriteLine();
                Console.WriteLine("Model saved to " + result.ModelPath);
                Console.WriteLine(
                    "Alternating-side minimax benchmark: "
                    + result.Snapshot.Evaluation.Score.ToString("P1")
                    + " (target "
                    + result.Snapshot.TargetScore.ToString("P0")
                    + ")");
                Console.WriteLine("Start the web app and select \"" + options.Name + "\" to play it.");
                return result.TargetReached ? 0 : 3;
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is InvalidDataException)
            {
                Console.Error.WriteLine("training failed: " + exception.Message);
                return 1;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }

        private static void WriteHelp(TextWriter writer)
        {
            writer.WriteLine("Train a reinforcement-learning Renzyu AI against minimax.");
            writer.WriteLine();
            writer.WriteLine("Usage:");
            writer.WriteLine("  dotnet run --project Trainer -- [options]");
            writer.WriteLine();
            writer.WriteLine("Options:");
            writer.WriteLine("  --name <text>             Model name shown in the browser");
            writer.WriteLine("  --episodes <n>            Training games (default: 500)");
            writer.WriteLine("  --learning-rate <n>       TD learning rate (default: 0.04)");
            writer.WriteLine("  --discount <n>            Future reward discount (default: 0.90)");
            writer.WriteLine("  --epsilon-start <n>       Initial exploration rate (default: 0.35)");
            writer.WriteLine("  --epsilon-end <n>         Final exploration rate (default: 0.03)");
            writer.WriteLine("  --opponent-depth <1-4>    Minimax benchmark depth (default: 4)");
            writer.WriteLine("  --opponent-nodes <n>      Minimax node budget (default: 60000)");
            writer.WriteLine("  --teacher-depth <1-6>     Guided exploration depth (default: 6)");
            writer.WriteLine("  --teacher-nodes <n>       Guided exploration budget (default: 120000)");
            writer.WriteLine("  --evaluate-every <n>      Episodes between evaluations (default: 25)");
            writer.WriteLine("  --evaluation-games <n>    Alternating-side games (default: 2)");
            writer.WriteLine("  --checkpoint-every <n>    Snapshot interval; 0 disables (default: 100)");
            writer.WriteLine("  --time-limit-seconds <n>  Hard training budget, max 300 (default: 300)");
            writer.WriteLine("  --target-score <n>        Early-stop benchmark, >0.5 (default: 0.75)");
            writer.WriteLine("  --seed <n>                Random seed (default: 1337)");
            writer.WriteLine("  --output <path>           Model directory (default: Host\\TrainedModels)");
            writer.WriteLine("  --no-dashboard            Print compact progress lines");
            writer.WriteLine("  -h, --help                Show this help");
        }
    }
}
