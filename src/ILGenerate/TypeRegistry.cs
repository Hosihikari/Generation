using Hosihikari.Generation.CppParser;
using System.Reflection.Emit;

namespace Hosihikari.Generation.ILGenerate;

public class TypeRegistry
{
    public AssemblyGenerator AssemblyGenerator { get; }

    public TypeRegistry(AssemblyGenerator assemblyGenerator)
    {
        if (assemblyGenerator.TypeRegistry is not null)
            throw new InvalidOperationException("TypeRegistry is already initialized");

        AssemblyGenerator = assemblyGenerator;
    }

    public async ValueTask<Type?> ResolveTypeAsync(CppType type)
    {
        throw new NotImplementedException();
    }
}
