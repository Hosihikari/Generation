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

    public List<MethodGenerator> Methods { get; } = [];
    public List<StaticFieldGenerator> StaticFields { get; } = [];

    public TypeBuilder TypeBuilder { get; }

    public TypeBuilder OriginalTypeBuilder { get; }

    private TypeGenerator(AssemblyGenerator assemblyGenerator, OriginalClass @class, CppType cppType)
    {
        AssemblyGenerator = assemblyGenerator;

        Class = @class;
        ParsedType = cppType;

        TypeBuilder = assemblyGenerator.MainModuleBuilder.DefineType(cppType.RootType.TypeIdentifier, TypeAttributes.Class | TypeAttributes.Public);
        OriginalTypeBuilder = TypeBuilder.DefineNestedType("Original", TypeAttributes.NestedPublic | TypeAttributes.Interface);
    }

    private TypeGenerator(AssemblyGenerator assemblyGenerator, CppType cppType)
    {
        AssemblyGenerator = assemblyGenerator;

        ParsedType = cppType;

        TypeBuilder = assemblyGenerator.MainModuleBuilder.DefineType(cppType.RootType.TypeIdentifier, TypeAttributes.Class | TypeAttributes.Public);
        OriginalTypeBuilder = TypeBuilder.DefineNestedType(
            "Original", 
            TypeAttributes.Interface |
            TypeAttributes.NestedPublic |
            TypeAttributes.AutoClass |
            TypeAttributes.AnsiClass |
            TypeAttributes.Abstract);
    }

    public static bool TryCreateTypeGenerator(AssemblyGenerator assemblyGenerator, string type, OriginalClass @class, [NotNullWhen(true)] out TypeGenerator? typeGenerator)
    {
        if (CppTypeParser.TryParse(type, out var cppType))
        {
            typeGenerator = new(assemblyGenerator, @class, cppType.RootType);
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
            typeGenerator = new(assemblyGenerator, cppType.RootType);
            return true;
        }
        else
        {
            typeGenerator = null;
            return false;
        }
    }

    public static bool TryCreateTypeGenerator(AssemblyGenerator assemblyGenerator, CppType type, OriginalClass @class, [NotNullWhen(true)] out TypeGenerator? typeGenerator)
    {
        typeGenerator = new(assemblyGenerator, @class, type.RootType);
        return true;
    }

    public static bool TryCreateEmptyTypeGenerator(AssemblyGenerator assemblyGenerator, CppType type, [NotNullWhen(true)] out TypeGenerator? typeGenerator)
    {
        typeGenerator = new(assemblyGenerator, type.RootType);
        return true;
    }

    public async ValueTask<bool> GenerateAsync()
    {
        if (Class is null)
            return false;

        var itemsAndAccessType = Class.GetAllItemsWithAccessType();
        foreach (var (accessType, isStatic, items) in itemsAndAccessType)
        {
            if (items is null)
                continue;

            foreach (var item in items)
            {
                if ((SymbolType)item.SymbolType == SymbolType.StaticField)
                {
                    //StaticFieldGenerator.TryCreateStaticFieldGenerator(item, this, out var staticFieldGenerator);
                }
                else
                {
                    if (MethodGenerator.TryCreateMethodGenerator(accessType, isStatic, item, this, out var methodGenerator))
                    {
                        await methodGenerator.GenerateAsync();
                        Methods.Add(methodGenerator);
                    }
                }
            }
        }

        return true;
    }
}
