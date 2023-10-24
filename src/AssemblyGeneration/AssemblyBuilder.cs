global using OC = Mono.Cecil.Cil.OpCodes;

using Hosihikari.Generation.Generator;
using Hosihikari.Utils;
using Mono.Cecil;
using System.Diagnostics.CodeAnalysis;
using static Hosihikari.Utils.OriginalData.Class;

namespace Hosihikari.Generation.AssemblyGeneration;

/// <summary>
/// <code>
/// public class Type
/// {
///     public interface ITypeOriginal
///     {
///         private static class __fptrStorageType
///         {
///             static __fptrStorageType() => _fptr = GetfptrFromSymbol("functionSymbol");
///             
///             // initialized by clr static ctor.
///             public static delegate* unmanaged<nint, ...> _fptr;
///         }
/// 
///         [MethodImpl(MethodImplOptions.AggressiveInlining)]
///         public static delegate* unmanaged<nint, ...> fptr => __fptrStorageType._fptr;
///     }
///     
///     [MethodImpl(MethodImplOptions.AggressiveInlining)]
///     public Function() => ITypeOriginal.fptr(this.Pointer, ...);
/// }
/// </code>
/// </summary>
public partial class AssemblyBuilder
{
    private readonly string name;
    private readonly AssemblyDefinition assembly;
    private readonly ModuleDefinition module;
    private readonly string outputDir;
    internal readonly Dictionary<string, TypeDefinition> definedTypes;
    internal readonly Dictionary<TypeDefinition, TypeBuilder> builders;
    internal readonly Queue<TypeBuilder> typeBuilders;

    public enum ItemAccessType
    {
        PublicStatic,
        PrivateStatic,
        Public,
        Protected,
        Virtual,
        VirtualUnordered,
    }

    public AssemblyBuilder(AssemblyDefinition assembly, string outputDir, string name)
    {
        this.assembly = assembly;
        module = assembly.MainModule;
        this.outputDir = outputDir;
        definedTypes = new();
        typeBuilders = new();
        builders = new();
        this.name = name;

        InsertAttributes();
    }

    public static AssemblyBuilder Create(string name, Version version, string outputDir, string? moduleName = null)
    {
        var assemblyDef = AssemblyDefinition.CreateAssembly(new(name, version), moduleName ?? name, ModuleKind.Dll);
        return new(assemblyDef, outputDir, name);
    }

    private void InsertAttributes()
    {
        module.Runtime = TargetRuntime.Net_4_0;

        CustomAttributeArgument arg = new(module.TypeSystem.String, ".NETCoreApp,Version=v7.0");
        CustomAttributeArgument frameworkDisplayName = new(module.TypeSystem.String, ".NET 7.0");
        CustomAttributeNamedArgument namedArgument = new("FrameworkDisplayName", frameworkDisplayName);

        CustomAttribute attribute = new(
            module.ImportReference(
                typeof(System.Runtime.Versioning.TargetFrameworkAttribute)
                .GetConstructors()
                .First(c => c.GetParameters().Length is 1)));

        attribute.ConstructorArguments.Add(arg);
        attribute.Properties.Add(namedArgument);

        assembly.CustomAttributes.Add(attribute);
    }

    public void Build(in OriginalData data)
    {
        ForeachClassesAndBuildTypeDefinition(data);
        BuildTypes();
    }

    public bool TryCreateTypeBuilder(in TypeData typeData, [NotNullWhen(true)] out TypeBuilder? builder)
    {
        builder = null;

        //not impl
        if (typeData.Namespaces.Count is not 0)
            return false;

        var type = typeData.Analyzer.CppTypeHandle.RootType;
        switch (type.Type)
        {
            case CppTypeEnum.Class:
            case CppTypeEnum.Struct:
            case CppTypeEnum.Union:

                var definition = new TypeDefinition("Hosihikari.Minecraft", typeData.TypeIdentifier, TypeAttributes.Public | TypeAttributes.Class);
                builder = new(definedTypes, null, module, definition, null, 0);
                typeBuilders.Enqueue(builder);
                builders.Add(definition, builder);
                return true;

            default:
                return false;
        }
    }

    public void ForeachItemsForBuildTypeDefinition(List<(ItemAccessType, Item, int?)> items, List<Item>? list, ItemAccessType accessType, bool isVirt = false)
    {
        if (list is null) return;

        for (int i = 0; i < list.Count; i++)
        {
            Item item = list[i];
            try
            {

                if (string.IsNullOrEmpty(item.Type.Name) is false)
                {
                    var typeData = new TypeData(item.Type);
                    if (definedTypes.ContainsKey(typeData.FullTypeIdentifier) is false)
                    {
                        if (TryCreateTypeBuilder(typeData, out var builder))
                        {
                            module.Types.Add(builder.definition);
                            definedTypes.Add(typeData.FullTypeIdentifier, builder.definition);
                        }
                    }
                }

                if (item.Params is not null)
                {
                    foreach (var param in item.Params)
                    {
                        var typeData = new TypeData(param);
                        if (definedTypes.ContainsKey(typeData.FullTypeIdentifier) is false)
                        {
                            if (TryCreateTypeBuilder(typeData, out var builder))
                            {
                                module.Types.Add(builder.definition);
                                definedTypes.Add(typeData.FullTypeIdentifier, builder.definition);
                            }
                        }
                    }
                }

                items.Add((accessType, item, isVirt ? i : null));
            }
            catch (Exception) { continue; }
        }
    }

    public void ForeachClassesAndBuildTypeDefinition(in OriginalData data)
    {
        foreach (var @class in data.Classes)
        {
            try
            {
                TypeDefinition definition;
                {
                    var typeData = new TypeData(new() { Name = @class.Key });
                    if (definedTypes.TryGetValue(typeData.FullTypeIdentifier, out var type) is false)
                    {
                        if (TryCreateTypeBuilder(typeData, out var builder))
                        {
                            type = builder.definition;
                            module.Types.Add(type);
                            definedTypes.Add(typeData.FullTypeIdentifier, type);
                        }
                    }
                    definition = type!;
                }

                var arr = new List<Item>?[]
                {
                @class.Value.PublicStatic,
                @class.Value.PrivateStatic,
                @class.Value.Public,
                @class.Value.Protected,
                @class.Value.Virtual
                };



                if (definition is not null)
                {
                    var items = new List<(ItemAccessType, Item, int?)>();
                    ForeachItemsForBuildTypeDefinition(items, @class.Value.PublicStatic, ItemAccessType.PublicStatic);
                    ForeachItemsForBuildTypeDefinition(items, @class.Value.PrivateStatic, ItemAccessType.PrivateStatic);
                    ForeachItemsForBuildTypeDefinition(items, @class.Value.Public, ItemAccessType.Public);
                    ForeachItemsForBuildTypeDefinition(items, @class.Value.Protected, ItemAccessType.Protected);
                    ForeachItemsForBuildTypeDefinition(items, @class.Value.Virtual, ItemAccessType.Virtual, true);
                    ForeachItemsForBuildTypeDefinition(items, @class.Value.VirtualUnordered, ItemAccessType.VirtualUnordered);

                    if (builders.TryGetValue(definition, out var builder))
                    {
                        builder.SetItems(items);
                        builder.SetVirtualFunctrions(@class.Value.Virtual);
                        builder.SetClassSize(0);
                    }
                }
            }
            catch (Exception) { continue; }
        }
    }

    public void BuildTypes()
    {
        while (typeBuilders.TryDequeue(out var builder))
        {
            builder.Build();
        }
    }

    public void Wirte()
    {
        assembly.Write(Path.Combine(outputDir, $"{name}.dll"));
    }
}
