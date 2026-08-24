using ReleaseTwin.Cli;

var casesDirectory = args.Length > 0 ? args[0] : "cases";

var environment = Environment.GetEnvironmentVariables()
    .Cast<System.Collections.DictionaryEntry>()
    .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value);

var runner = new CliRunner();
return await runner.RunAsync(casesDirectory, environment, Console.Out);
