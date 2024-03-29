using Hosihikari.Generation.CppParser;
using Hosihikari.Generation.Utils;
using System.Reflection;
using System.Reflection.Emit;

namespace Hosihikari.Generation.ILGenerate;

public class AssemblyGenerator
{
    private OriginalData OriginalData { get; }

    public AssemblyBuilder AssemblyBuilder { get; }
    public ModuleBuilder MainModuleBuilder { get; }

    public TypeRegistry TypeRegistry { get; }

    private AssemblyGenerator(OriginalData originalData, AssemblyName assemblyName)
    {
        this.OriginalData = originalData;
        AssemblyBuilder = AssemblyBuilder.DefinePersistedAssembly(assemblyName, typeof(object).Assembly);
        MainModuleBuilder = AssemblyBuilder.DefineDynamicModule("MainModule");

        TypeRegistry = new(this);
    }

    public static bool TryCreateGenerator(OriginalData originalData, AssemblyName assemblyName, out AssemblyGenerator? generator)
    {
        generator = new(originalData, assemblyName);
        return true;
    }

    public async ValueTask<bool> GenerateAsync()
    {
        throw new NotImplementedException();

        foreach (var (typeStr, @class) in OriginalData.Classes)
        {
            if (!TypeGenerator.TryCreateTypeGenerator(typeStr, @class, this, out var typeGenerator))
                continue;

        }
    }

    public async ValueTask SaveAsync(FileInfo file)
    {
        throw new NotImplementedException();
    }
}