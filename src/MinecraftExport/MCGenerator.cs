using Hosihikari.Generation.ILGenerate;
using Hosihikari.Generation.Utils;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;

namespace Hosihikari.Generation.MinecraftExport;

internal sealed class McGenerator : IGenerator
{
    #region ---Constructor---

    public McGenerator(string originalFilePath)
    {
        string originalDataJson = File.ReadAllText(originalFilePath);
        originalData = JsonSerializer.Deserialize<OriginalData>(originalDataJson) ??
                       throw new InvalidDataException("Incorrect original data file!");
        //AssemblyName assemblyName = new("Hosihikari.Minecraft");
        //assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess. /* Save */ Run);
        //moduleBuilder = assemblyBuilder.DefineDynamicModule("Hosihikari.Minecraft");
    }

    #endregion

    #region ---Private method---

    private void HandleMembers(OriginalItem[] member)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region ---Private field---

    //private readonly AssemblyBuilder assemblyBuilder;
    //private readonly ModuleBuilder moduleBuilder;
    private readonly OriginalData originalData;

    private AssemblyGenerator? assemblyGenerator;

    #endregion

    #region ---Public method---

    [MemberNotNull(nameof(assemblyGenerator))]
    public void Initialize()
    {
        //using StreamWriter logFile = File.AppendText($"SkipedClass-{DateTime.Now:yyyy-M-dTHH-mm-ss}.log");
        //foreach ((string name, _) in originalData.Classes)
        //{
        //    if (Shared.SkippedOnContain.Any(str => name.Contains(str)))
        //    {
        //        logFile.WriteLine(name);
        //        continue;
        //    }

        //    moduleBuilder.DefineType(name);
        //}
        AssemblyGenerator.TryCreateGenerator(originalData, new AssemblyName("Hosihikari.Minecraft"), out assemblyGenerator);
    }

    public async ValueTask RunAsync()
    {
        if (assemblyGenerator is not null)
            await assemblyGenerator.GenerateAsync();
    }

    public async ValueTask SaveAsync(string path)
    {
        if(assemblyGenerator is not null)
        await assemblyGenerator!.SaveAsync(new FileInfo(path));
    }

    #endregion
}