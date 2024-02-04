global using OC = Mono.Cecil.Cil.OpCodes;
using Hosihikari.Generation.Generator;
using Hosihikari.Generation.Parser;
using Hosihikari.Generation.Utils;
using Mono.Cecil;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using static Hosihikari.Generation.Utils.OriginalData.Class;

namespace Hosihikari.Generation.AssemblyGeneration;

/// <summary>
///     <code>
/// public class Type
/// {
///     public interface ITypeOriginal
///     {
///         private static class __fptrStorageType
///         {
///             static __fptrStorageType() => _fptr = GetfptrFromSymbol("functionSymbol");
///             
///             // initialized by clr static ctor.
///             public static delegate* unmanaged<nint, ...>
///             _fptr;
///             }
///             [MethodImpl(MethodImplOptions.AggressiveInlining)]
///             public static delegate* unmanaged
///             <nint, ...>
///                 fptr => __fptrStorageType._fptr;
///                 }
///                 [MethodImpl(MethodImplOptions.AggressiveInlining)]
///                 public Function() => ITypeOriginal.fptr(this.Pointer, ...);
///                 }
/// </code>
/// </summary>
public class AssemblyBuilder
{
    public enum ItemAccessType
    {
        PublicStatic,
        PrivateStatic,
        Public,
        Protected,
        Private,
        Virtual,
        VirtualUnordered
    }

    private const string RootNamespace = "Hosihikari.Minecraft";
    private readonly AssemblyDefinition assembly;
    private readonly Dictionary<TypeDefinition, (TypeData typData, TypeBuilder builder)> builders;
    private readonly Dictionary<string, TypeDefinition> definedTypes;
    private readonly ModuleDefinition module;

    private readonly string name;
    private readonly string outputDir;
    private readonly Dictionary<TypeDefinition, PredefinedTypeExtensionBuilder> predefinedBuilders;
    private readonly Queue<PredefinedTypeExtensionBuilder> predefinedTypeBuilders;

    private readonly NamespaceNode rootNamespace;
    private readonly Queue<TypeBuilder> typeBuilders;

    private AssemblyBuilder(AssemblyDefinition assembly, string outputDir, string name)
    {
        this.assembly = assembly;
        module = assembly.MainModule;
        this.outputDir = outputDir;
        definedTypes = new();
        typeBuilders = new();
        predefinedBuilders = new();
        builders = new();
        predefinedTypeBuilders = new();
        this.name = name;
        rootNamespace = new RootNamespaceNode(module);

        InsertAttributes();
    }

    private void InsertTypeIntoNamespaces(in TypeData typeData, TypeDefinition definition)
    {
        if (typeData.Namespaces.Count is not 0)
        {
            NamespaceNode namespaceNode = rootNamespace;

            for (int i = 0; i < typeData.Namespaces.Count; i++)
            {
                string @namespace = typeData.Namespaces[i];

                if (namespaceNode.SubNamespaces.TryGetValue(@namespace, out NamespaceNode? node) is false)
                {
                    string typeStr =
                        $"{string.Join('.', typeData.Namespaces.Take(i))}{(i > 0 ? "." : string.Empty)}{@namespace}";
                    if (definedTypes.TryGetValue(typeStr, out TypeDefinition? definedType) is false)
                    {
                        if (TryCreateTypeBuilder(
                                new(new()
                                {
                                    Name =
                                        $"{string.Join("::", typeData.Namespaces.Take(i))}{(i > 0 ? "::" : string.Empty)}{@namespace}"
                                }),
                                out TypeBuilder? builder))
                        {
                            definedType = builder.definition;
                        }
                        else
                        {
                            throw new InvalidDataException();
                        }
                    }

                    node = new(module, definedType);
                    namespaceNode.SubNamespaces.Add(@namespace, node);
                }

                namespaceNode = node;

                if (i != (typeData.Namespaces.Count - 1))
                {
                    continue;
                }

                node.Types.Add(definition);
                return;
            }
        }
        else
        {
            rootNamespace.Types.Add(definition);
        }
    }

    public static AssemblyBuilder Create(string name, Version version, string outputDir, string? moduleName = null)
    {
        AssemblyDefinition? assemblyDef =
            AssemblyDefinition.CreateAssembly(new(name, version), moduleName ?? name, ModuleKind.Dll);

        return new(assemblyDef, outputDir, name);
    }

    private void InsertAttributes()
    {
        module.Runtime = TargetRuntime.Net_4_0;

        CustomAttributeArgument arg = new(module.ImportReference(Utils.String), ".NETCoreApp,Version=v8.0");
        CustomAttributeArgument frameworkDisplayName = new(module.ImportReference(Utils.String), ".NET 8.0");
        CustomAttributeNamedArgument namedArgument = new("FrameworkDisplayName", frameworkDisplayName);

        CustomAttribute attribute = new(
            module.ImportReference(
                typeof(TargetFrameworkAttribute)
                    .GetConstructors()
                    .First(c => c.GetParameters().Length is 1)));

        attribute.ConstructorArguments.Add(arg);
        attribute.Properties.Add(namedArgument);

        assembly.CustomAttributes.Add(attribute);
    }

    public void Build(in OriginalData data)
    {
        ForeachClassesAndBuildTypeDefinition(data);
        ForeachNamespacesForBuildNestedTypes();
        BuildTypes();
    }

    private void ForeachNamespacesForBuildNestedTypes()
    {
        ForeachNamespacesForBuildNestedTypesInternal(rootNamespace);
    }

    private static void ForeachNamespacesForBuildNestedTypesInternal(NamespaceNode node)
    {
        foreach ((string _, NamespaceNode subNode) in node.SubNamespaces)
        {
            ForeachNamespacesForBuildNestedTypesInternal(subNode);
        }

        foreach (TypeDefinition type in node.Types)
        {
            node.AddType(type);
        }
    }

    private bool TryCreateTypeBuilder(in TypeData typeData, [NotNullWhen(true)] out TypeBuilder? builder)
    {
        builder = null;

        CppTypeNode type = typeData.Analyzer.CppTypeHandle.RootType;
        switch (type.Type)
        {
            case CppTypeEnum.Class:
            case CppTypeEnum.Struct:
            case CppTypeEnum.Union:

                string typeIdentifier = typeData.TypeIdentifier;
                if (typeData.Analyzer.CppTypeHandle.RootType.IsTemplate)
                {
                    typeIdentifier = typeData.Analyzer.CppTypeHandle.RootType.TemplateTypes!.Aggregate(typeIdentifier,
                        (current, templateType) => current + $"_{templateType.RootType.TypeIdentifier}");
                }

                TypeDefinition definition = new(string.Empty, typeIdentifier,
                    TypeAttributes.Class, module.ImportReference(Utils.Object));
                builder = new(definedTypes, null, module, definition, null, 0);
                typeBuilders.Enqueue(builder);
                builders.Add(definition, (typeData, builder));

                InsertTypeIntoNamespaces(typeData, definition);
                definedTypes.Add(typeData.FullTypeIdentifier, definition);

                return true;

            case CppTypeEnum.FundamentalType:
            case CppTypeEnum.Pointer:
            case CppTypeEnum.Ref:
            case CppTypeEnum.RValueRef:
            case CppTypeEnum.Enum:
            case CppTypeEnum.Array:
            case CppTypeEnum.VarArgs:
            default:
                return false;
        }
    }

    private bool TryCreatePredefinedTypeBuilder(in TypeData typeData, Type predefinedType,
        [NotNullWhen(true)] out PredefinedTypeExtensionBuilder? builder)
    {
        builder = null;

        CppTypeNode type = typeData.Analyzer.CppTypeHandle.RootType;
        switch (type.Type)
        {
            case CppTypeEnum.Class:
            case CppTypeEnum.Struct:
            case CppTypeEnum.Union:

                TypeDefinition definition = new("Hosihikari.Minecraft.Extension", $"{predefinedType.Name}MinecraftExtension",
                    TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract,
                    module.ImportReference(Utils.Object));
                builder = new(predefinedType, definedTypes, null, module, definition, null);
                predefinedTypeBuilders.Enqueue(builder);
                predefinedBuilders.Add(definition, builder);

                return true;

            case CppTypeEnum.FundamentalType:
            case CppTypeEnum.Pointer:
            case CppTypeEnum.Ref:
            case CppTypeEnum.RValueRef:
            case CppTypeEnum.Enum:
            case CppTypeEnum.Array:
            case CppTypeEnum.VarArgs:
            default:
                return false;
        }
    }

    private void CreateBuilderAndAddTypeDefinition(in TypeData typeData, out object? builder)
    {
        builder = null;

        if (definedTypes.ContainsKey(typeData.FullTypeIdentifier))
        {
            return;
        }

        CppTypeNode node = typeData.Analyzer.CppTypeHandle.RootType;
        if (TypeReferenceBuilder.TryGetPredefinedType(node, node.Type is CppTypeEnum.Enum,
                out Type? predefinedType))
        {
            if (!TryCreatePredefinedTypeBuilder(typeData, predefinedType,
                    out PredefinedTypeExtensionBuilder? _builder))
            {
                return;
            }

            module.Types.Add(_builder.definition);
            definedTypes.Add(typeData.FullTypeIdentifier, _builder.definition);
            builder = _builder;
        }
        else
        {
            if (TryCreateTypeBuilder(typeData, out TypeBuilder? _builder))
            {
                builder = _builder;
            }
        }
    }

    private void ForeachItemsForBuildTypeDefinition(ICollection<(ItemAccessType, Item, int?)> items, List<Item>? list,
        ItemAccessType accessType, bool isVirt = false)
    {
        if (list is null)
        {
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            try
            {
                Item item = list[i];
                if (string.IsNullOrEmpty(item.Type.Name) is false)
                {
                    CreateBuilderAndAddTypeDefinition(new(item.Type), out object? _);
                }

                if (item.Params is not null)
                {
                    foreach (Item.TypeData param in item.Params)
                    {
                        CreateBuilderAndAddTypeDefinition(new(param), out object? _);
                    }
                }

                items.Add((accessType, item, isVirt ? i : null));
            }
            catch { /*0.o*/ }

        }
    }

    private void ForeachClassesAndBuildTypeDefinition(in OriginalData data)
    {
        foreach ((string? key, OriginalData.Class value) in data.Classes)
        {
            try
            {
                TypeData typeData = new(new() { Name = key });
                if (definedTypes.TryGetValue(typeData.FullTypeIdentifier, out TypeDefinition? type) is false)
                {
                    CreateBuilderAndAddTypeDefinition(typeData, out object? builder);
                    switch (builder)
                    {
                        case TypeBuilder typeBuilder:
                            type = typeBuilder.definition;
                            break;
                        case PredefinedTypeExtensionBuilder predefinedBuilder:
                            type = predefinedBuilder.definition;
                            break;
                        default:
                            continue;
                    }
                }

                TypeDefinition definition = type;


                List<(ItemAccessType, Item, int?)> items = [];

                if (builders.TryGetValue(definition, out (TypeData typData, TypeBuilder builder) pair))
                {
                    (_, TypeBuilder builder) = pair;

                    builder.SetItems(items);
                    builder.SetVirtualFunctrions(value.Virtual);
                    builder.SetClassSize(0);
                }
                else if (predefinedBuilders.TryGetValue(definition,
                             out PredefinedTypeExtensionBuilder? predefinedBuilder))
                {
                    predefinedBuilder.SetItems(items);
                    predefinedBuilder.SetVirtualFunctrions(value.Virtual);
                }

                ForeachItemsForBuildTypeDefinition(items, value.PublicStatic, ItemAccessType.PublicStatic);
                ForeachItemsForBuildTypeDefinition(items, value.PrivateStatic, ItemAccessType.PrivateStatic);
                ForeachItemsForBuildTypeDefinition(items, value.Public, ItemAccessType.Public);
                ForeachItemsForBuildTypeDefinition(items, value.Protected, ItemAccessType.Protected);
                ForeachItemsForBuildTypeDefinition(items, value.Private, ItemAccessType.Private);
                ForeachItemsForBuildTypeDefinition(items, value.Virtual, ItemAccessType.Virtual, true);
                ForeachItemsForBuildTypeDefinition(items, value.VirtualUnordered,
                    ItemAccessType.VirtualUnordered);
            }
            catch
            {
                // ignored
            }
        }
    }

    private void BuildTypes()
    {
        while (typeBuilders.TryDequeue(out TypeBuilder? builder))
        {
            builder.Build();
        }

        while (predefinedTypeBuilders.TryDequeue(out PredefinedTypeExtensionBuilder? predefinedBuilder))
        {
            predefinedBuilder.Build();
        }
    }

    public void Write()
    {
        assembly.Write(Path.Combine(outputDir, $"{name}.dll"));
    }

    public class NamespaceNode(
        ModuleDefinition module,
        TypeDefinition? namespaceType)
    {
        protected readonly ModuleDefinition Module = module;

        public Dictionary<string, NamespaceNode> SubNamespaces { get; } = new();

        public HashSet<TypeDefinition> Types { get; } = [];

        private TypeDefinition? NamespaceType { get; } = namespaceType;

        public virtual void AddType(TypeDefinition definition)
        {
            definition.Attributes |= TypeAttributes.NestedPublic;
            NamespaceType!.NestedTypes.Add(definition);
        }
    }

    private class RootNamespaceNode(ModuleDefinition module) : NamespaceNode(module, null!)
    {
        public override void AddType(TypeDefinition definition)
        {
            definition.Namespace = RootNamespace;
            definition.Attributes |= TypeAttributes.Public;
            Module.Types.Add(definition);
        }
    }
}