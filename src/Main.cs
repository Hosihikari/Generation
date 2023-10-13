using Hosihikari.Utils;
using System.Text.Json;

namespace Hosihikari.Generation;

public class Main
{
    public static void Run(Config config)
    {
        var builder = AssemblyBuilder.Create("Test", new(1, 0, 0), config.AssemblyOutputDir);
        builder.Build(JsonSerializer.Deserialize<OriginalData>(File.ReadAllText(config.OriginalDataPath)));
        builder.Wirte();
    }
}
