using Hosihikari.Generation.Utils;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;

namespace Hosihikari.Generation.MinecraftExport;

internal sealed class McGenerator : IGenerator
{
    private readonly AssemblyBuilder assemblyBuilder;
    private OriginalData originalData;

    public McGenerator(string originalFilePath)
    {
        string originalDataJson = File.ReadAllText(originalFilePath);
        originalData = JsonSerializer.Deserialize<OriginalData>(originalDataJson);
        AssemblyName assemblyName = new("Hosihikari.Minecraft");
        assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess. /* Save */ Run);
    }

    public void Initialize()
    {
        throw new NotImplementedException();
    }

    public void Run()
    {
        throw new NotImplementedException();
    }

    public void Save(string path)
    {
        assemblyBuilder.Save(path);
    }
}