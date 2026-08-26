using ReleaseTwin.Cli;

var environment = Environment.GetEnvironmentVariables()
    .Cast<System.Collections.DictionaryEntry>()
    .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value);

var runner = new CliRunner();

// --journey <journeyId>@<version> runs a pinned hosted journey instead of a local cases directory.
if (args.Length >= 2 && args[0] == "--journey")
{
    var parts = args[1].Split('@', 2);
    if (parts.Length != 2 || !Guid.TryParse(parts[0], out var journeyId) || !int.TryParse(parts[1], out var version))
    {
        Console.Out.WriteLine("--journey expects <journeyId>@<version>, e.g. --journey 3fa85f64-5717-4562-b3fc-2c963f66afa6@3");
        return 1;
    }

    return await runner.RunJourneyAsync(journeyId, version, environment, Console.Out);
}

var casesDirectory = args.Length > 0 ? args[0] : "cases";
return await runner.RunAsync(casesDirectory, environment, Console.Out);
