using Hosihikari.Generation;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.Diagnostics;

Option<string> pathOption = new("--path", "The path of original data");
Option<string> outputPathOption = new("--out", "The path of the output directory");
Option<string> versionOption = new("--mcver", "The version of Minecraft");
RootCommand rootCommand = [pathOption, outputPathOption, versionOption];
rootCommand.SetHandler((path, outputPath, version) =>
{
    using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
    ILogger logger = factory.CreateLogger(nameof(Main));
    Stopwatch watcher = new();
    logger.LogInformation("Start generating at {DateTime}", DateTime.Now);
    watcher.Start();
    Main.Run(new(path, outputPath, Version.Parse(version)));
    watcher.Stop();
    logger.LogInformation("Generated successfully at {DateTime}, which took {TimeSpan}", DateTime.Now, watcher.Elapsed);
}, pathOption, outputPathOption, versionOption);
rootCommand.Invoke(args);