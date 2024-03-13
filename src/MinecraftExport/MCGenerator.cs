using Hosihikari.Generation.Utils;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;

namespace Hosihikari.Generation.MinecraftExport;

internal sealed class McGenerator : GeneratorBase
{
    #region ---Constructor---

    public McGenerator(string originalFilePath) : base(originalFilePath)
    {
        string originalDataJson = File.ReadAllText(originalFilePath);
        originalData = JsonSerializer.Deserialize<OriginalData>(originalDataJson) ??
                       throw new InvalidDataException("Incorrect original data file!");
        AssemblyName assemblyName = new("Hosihikari.Minecraft");
        assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess. /* Save */ Run);
        moduleBuilder = assemblyBuilder.DefineDynamicModule("Hosihikari.Minecraft");
    }

    #endregion

    #region ---Private method---

    private void HandleMembers(Item[] member)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region ---Private field---

    private readonly AssemblyBuilder assemblyBuilder;
    private readonly ModuleBuilder moduleBuilder;
    private readonly OriginalData originalData;

    #endregion

    #region ---Public method---

    public override void Initialize()
    {
        using StreamWriter logFile = File.AppendText($"SkipedClass-{DateTime.Now:yyyy-M-dTHH-mm-ss}.log");
        foreach ((string name, _) in originalData.Classes)
        {
            if (Shared.SkippedOnContain.Any(str => name.Contains(str)))
            {
                logFile.WriteLine(name);
                continue;
            }

            moduleBuilder.DefineType(name);
        }
    }

    public override void Run()
    {
        throw new NotImplementedException();
    }

    public override void Save(string path)
    {
        assemblyBuilder.Save(path);
    }

    #endregion
}