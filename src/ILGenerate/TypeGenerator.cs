using Hosihikari.Generation.CppParser;
using Hosihikari.Generation.Utils;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;

namespace Hosihikari.Generation.ILGenerate;

public class TypeGenerator
{
    public AssemblyGenerator AssemblyGenerator { get; }

    public OriginalClass? Class { get; }

    public CppType ParsedType { get; }

    public List<MethodBuilder> Methods { get; } = [];
    public List<StaticFieldGenerator> StaticFields { get; } = [];

    public TypeBuilder TypeBuilder { get; }

    private TypeGenerator(AssemblyGenerator assemblyGenerator, OriginalClass @class, CppType cppType)
    {
        AssemblyGenerator = assemblyGenerator;

        Class = @class;
        ParsedType = cppType;

        TypeBuilder = assemblyGenerator.MainModuleBuilder.DefineType(cppType.RootType.TypeIdentifier, TypeAttributes.Class | TypeAttributes.Public);
    }

    private TypeGenerator(AssemblyGenerator assemblyGenerator, CppType cppType)
    {
        AssemblyGenerator = assemblyGenerator;

        ParsedType = cppType;

        TypeBuilder = assemblyGenerator.MainModuleBuilder.DefineType(cppType.RootType.TypeIdentifier, TypeAttributes.Class | TypeAttributes.Public);
    }

    public static bool TryCreateTypeGenerator(string type, OriginalClass @class, AssemblyGenerator assemblyGenerator, [NotNullWhen(true)] out TypeGenerator? typeGenerator)
    {
        if (CppTypeParser.TryParse(type, out var cppType))
        {
            typeGenerator = new(assemblyGenerator, @class, cppType);
            return true;
        }
        else
        {
            typeGenerator = null;
            return false;
        }
    }

    public static bool TryCreateEmptyTypeGenerator(AssemblyGenerator assemblyGenerator, string type, [NotNullWhen(true)] out TypeGenerator? typeGenerator)
    {
        if (CppTypeParser.TryParse(type, out var cppType))
        {
            typeGenerator = new(assemblyGenerator, cppType);
            return true;
        }
        else
        {
            typeGenerator = null;
            return false;
        }
    }
}
