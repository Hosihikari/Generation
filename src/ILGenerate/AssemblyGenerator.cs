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
        OriginalData = originalData;
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
        try
        {
            foreach (var (typeStr, @class) in OriginalData.Classes)
            {
                if (CppTypeParser.TryParse(typeStr, out var cppType) is false)
                    continue;

                var generator = await TypeRegistry.RegisterTypeAsync(cppType, @class);
                if (generator is null)
                    continue;

                await generator.GenerateAsync();
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async ValueTask SaveAsync(FileInfo file)
        => await Task.Run(() => AssemblyBuilder.Save(file.FullName));
}