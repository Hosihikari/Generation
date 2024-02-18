using Hosihikari.Generation.AssemblyGeneration;
using Hosihikari.Generation.LeviLaminaExportGeneration;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.Diagnostics;

using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
ILogger logger = factory.CreateLogger(nameof(Hosihikari.Generation));

Option<string> pathOption = new("--path", "The path of original data");
Option<string> outputPathOption = new("--out", "The path of the output directory");
Option<string> dotnetSdkDirOption = new("--dotnetsdkdir", "The directory of dotnet sdk");
Option<string> refAssemblyDirOption = new("--refasmdir", "The directory of reference assemblies");
Option<string> versionOption = new("--mcver", "The version of Minecraft");
Command mcExportCommnad = new("mcexport");
mcExportCommnad.AddOption(pathOption);
mcExportCommnad.AddOption(outputPathOption);
mcExportCommnad.AddOption(dotnetSdkDirOption);
mcExportCommnad.AddOption(refAssemblyDirOption);
mcExportCommnad.AddOption(versionOption);

mcExportCommnad.SetHandler((path, outputPath, dotnetSdkDir, refAssemblyDir, version) =>
{
    Stopwatch watcher = new();
    logger.LogInformation("Start generating at {DateTime}", DateTime.Now);
    watcher.Start();
    AssemblyGeneration.Run(new(path, outputPath, dotnetSdkDir, refAssemblyDir, Version.Parse(version)));
    watcher.Stop();
    logger.LogInformation("Generated successfully at {DateTime}, which took {TimeSpan}", DateTime.Now, watcher.Elapsed);
}, pathOption, outputPathOption, dotnetSdkDirOption, refAssemblyDirOption, versionOption);


Option<string> sourceDirOption = new("--sourcedir", "The directory of the source code");
Command leviLaminaExportCommnad = new("leviexport");
leviLaminaExportCommnad.AddOption(sourceDirOption);
leviLaminaExportCommnad.AddOption(outputPathOption);

leviLaminaExportCommnad.SetHandler((sourceDir, outputPath) =>
{
    Stopwatch watcher = new();
    logger.LogInformation("Start generating at {DateTime}", DateTime.Now);
    watcher.Start();
    LeviLaminaExportGeneration.Run(sourceDir, outputPath);
    watcher.Stop();
    logger.LogInformation("Generated successfully at {DateTime}, which took {TimeSpan}", DateTime.Now, watcher.Elapsed);
}, sourceDirOption, outputPathOption);

RootCommand rootCommand = [mcExportCommnad, leviLaminaExportCommnad];

rootCommand.Invoke(args);