using ReleaseTwin.Cli;

var environment = Environment.GetEnvironmentVariables()
    .Cast<System.Collections.DictionaryEntry>()
    .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value);

return await CliEntrypoint.RunAsync(args, environment, Console.Out);
