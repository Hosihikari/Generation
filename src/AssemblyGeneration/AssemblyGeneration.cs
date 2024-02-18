using Hosihikari.Generation.Utils;
using System.Text.Json;

namespace Hosihikari.Generation.AssemblyGeneration;

public static class AssemblyGeneration
{
    public static void Run(Config config)
    {
        Utils.Init(config);
        TypeReferenceBuilder.Init(config);

        AssemblyBuilder builder =
            AssemblyBuilder.Create("Hosihikari.Minecraft", config.AssemblyVersion, config.AssemblyOutputDir);
        builder.Build(JsonSerializer.Deserialize<OriginalData>(File.ReadAllText(config.OriginalDataPath)));
        builder.Write();
    }
}