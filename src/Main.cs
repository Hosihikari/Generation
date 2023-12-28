using System.Text.Json;
using Hosihikari.Generation.AssemblyGeneration;
using Hosihikari.Generation.Utils;

namespace Hosihikari.Generation;

public static class Main
{
    public static void Run(Config config)
    {
        AssemblyBuilder builder =
            AssemblyBuilder.Create("Hosihikari.Minecraft", new(1, 0, 0), config.AssemblyOutputDir);
        builder.Build(JsonSerializer.Deserialize<OriginalData>(File.ReadAllText(config.OriginalDataPath)));
        builder.Write();
    }
}