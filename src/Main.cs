using Hosihikari.Generation.AssemblyGeneration;
using Hosihikari.Generation.Utils;
using System.Text.Json;

namespace Hosihikari.Generation;

public static class Main
{
    public static void Run(Config config)
    {
        AssemblyBuilder builder =
            AssemblyBuilder.Create("Hosihikari.Minecraft", config.AssemblyVersion, config.AssemblyOutputDir);
        builder.Build(JsonSerializer.Deserialize<OriginalData>(File.ReadAllText(config.OriginalDataPath)));
        builder.Write();
    }
}