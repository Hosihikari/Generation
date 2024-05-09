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

    public McGenerator(string originalFilePath, string systemRuntimeAssemblyPath, string referenceAssembliesDirectory)
    {
        string originalDataJson = File.ReadAllText(originalFilePath);
        originalData = JsonSerializer.Deserialize<OriginalData>(originalDataJson) ??
                       throw new InvalidDataException("Incorrect original data file!");

        this.systemRuntimeAssemblyPath = systemRuntimeAssemblyPath;
        this.referenceAssembliesDirectory = referenceAssembliesDirectory;
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

    private readonly string systemRuntimeAssemblyPath;

    private readonly string referenceAssembliesDirectory;

    #endregion

    #region ---Public method---

    public void Initialize()
    {

        AssemblyGenerator.TryCreateGenerator(
            originalData,
            new AssemblyName("Hosihikari.Minecraft"),
            systemRuntimeAssemblyPath,
            (from path in Directory.EnumerateFiles(referenceAssembliesDirectory)
             select Assembly.LoadFrom(path)).ToList(),
            out assemblyGenerator);
    }

    public async ValueTask RunAsync()
    {
        if (assemblyGenerator is not null)
            await assemblyGenerator.GenerateAsync();
    }

    public async ValueTask SaveAsync(string path)
    {
        if (assemblyGenerator is not null)
            await assemblyGenerator!.SaveAsync(new FileInfo(path));
    }

    #endregion
}