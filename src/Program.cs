using Hosihikari.Generation.LeviLaminaExport;
using Hosihikari.Generation.MinecraftExport;
using Hosihikari.Generation.Utils;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.Diagnostics;

namespace Hosihikari.Generation;

public static class Program
{
    public static async Task Main(string[] args)
    {
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
        ILogger logger = factory.CreateLogger(nameof(Generation));

        Option<OutPutType> typeOption = new("--type", "The type of the output");
        Option<string> inputPathOption = new("--in", "The path of the input");
        Option<string> outputPathOption = new("--out", "The path of the output directory");
        Option<string?> refPathOption = new("--ref", "The directory of reference assemblies");
        Option<string?> runtimePathOption = new("--runtime", "The directory of system runtime assembly");
        Option<string?> versionOption = new("--ver", "The version of the assembly");

        RootCommand rootCommand = [];
        rootCommand.Add(typeOption);
        rootCommand.Add(inputPathOption);
        rootCommand.Add(outputPathOption);
        rootCommand.Add(versionOption);
        rootCommand.Add(refPathOption);
        rootCommand.Add(runtimePathOption);

        rootCommand.SetHandler(async (type, inputPath, outputPath, refPath, runtimePath, version) =>
        {
            Stopwatch watcher = new();
            IGenerator generator = type switch
            {
                OutPutType.Minecraft => new McGenerator(
                    inputPath,
                    runtimePath ?? throw new ArgumentNullException(nameof(runtimePath)),
                    refPath ?? throw new ArgumentNullException(nameof(refPath))),
                OutPutType.LeviLamina => new LlGenerator(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
            logger.LogInformation("Start preparing at {DateTime}", DateTime.Now);
            generator.Initialize();
            logger.LogInformation("Start generating at {DateTime}", DateTime.Now);
            watcher.Start();
            await generator.RunAsync();
            watcher.Stop();
            logger.LogInformation("Generated successfully at {DateTime}, which took {TimeSpan}. Saving...",
                DateTime.Now, watcher.Elapsed);
            await generator.SaveAsync(outputPath);
            logger.LogInformation("Save finished at {DateTime}", DateTime.Now);
        }, typeOption, inputPathOption, outputPathOption, refPathOption, runtimePathOption, versionOption);

        await rootCommand.InvokeAsync(args);
    }
}