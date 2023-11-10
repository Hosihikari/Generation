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
    public const string RootNamespace = "Hosihikari.Minecraft";

    public readonly string name;
    public readonly AssemblyDefinition assembly;
    public readonly ModuleDefinition module;
    public readonly string outputDir;
    public readonly Dictionary<string, TypeDefinition> definedTypes;
    public readonly Dictionary<TypeDefinition, (TypeData typData, TypeBuilder builder)> builders;
    public readonly Queue<TypeBuilder> typeBuilders;
    public readonly Dictionary<TypeDefinition, PredefinedTypeExtensionBuilder> predefinedBuilders;
    public readonly Queue<PredefinedTypeExtensionBuilder> predefinedTypeBuilders;

    public readonly NamespaceNode rootNamespace;

    public class NamespaceNode
    {
        public string Namespace { get; private set; }

        public Dictionary<string, NamespaceNode> SubNamespaces { get; private set; }

        public NamespaceNode? Parent { get; private set; }

        public HashSet<TypeDefinition> Types { get; private set; }

        public TypeDefinition? NamespaceType { get; set; }

        protected readonly ModuleDefinition Module;

        public NamespaceNode(ModuleDefinition module, string @namespace, NamespaceNode? parent, TypeDefinition? namespaceType)
        {
            Module = module;
            Namespace = @namespace;
            Parent = parent;
            NamespaceType = namespaceType;
            SubNamespaces = new();
            Types = new();
        }

        public virtual void AddType(TypeDefinition definition)
        {
            definition.Attributes |= TypeAttributes.NestedPublic;
            NamespaceType!.NestedTypes.Add(definition);
        }
    }

    public class RootNamespaceNode : NamespaceNode
    {
        public RootNamespaceNode(ModuleDefinition module) : base(module, string.Empty, null, null!)
        {
        }

        public override void AddType(TypeDefinition definition)
        {
            definition.Namespace = RootNamespace;
            definition.Attributes |= TypeAttributes.Public;
            Module.Types.Add(definition);
        }
    }

    public void InsertTypeIntoNamespaces(in TypeData typeData, TypeDefinition definition)
    {
        if (typeData.Namespaces.Count is not 0)
        {
            NamespaceNode namespaceNode = rootNamespace;

            for (int i = 0; i < typeData.Namespaces.Count; i++)
            {
                var @namespace = typeData.Namespaces[i];

                if (namespaceNode.SubNamespaces.TryGetValue(@namespace, out var node) is false)
                {
                    var typeStr = $"{string.Join('.', typeData.Namespaces.Take(i))}{(i > 0 ? "." : "")}{@namespace}";
                    if (definedTypes.TryGetValue(typeStr, out TypeDefinition? definedType) is false)
                    {
                        if (TryCreateTypeBuilder(
                            new(new() { Name = $"{string.Join("::", typeData.Namespaces.Take(i))}{(i > 0 ? "." : "")}{@namespace}" }),
                            out var builder))
                        {
                            definedType = builder.definition;
                        }
                        else throw new Exception("QAQ");
                    }

                    node = new NamespaceNode(module, @namespace, namespaceNode, definedType);
                    namespaceNode.SubNamespaces.Add(@namespace, node);
                }

                namespaceNode = node;

                if (i != typeData.Namespaces.Count - 1)
                {
                    continue;
                }
                else
                {
                    node.Types.Add(definition);
                    return;
                }
            }
        }
        else
        {
            rootNamespace.Types.Add(definition);
        }
    }

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
        predefinedBuilders = new();
        builders = new();
        predefinedTypeBuilders = new();
        this.name = name;
#pragma warning disable CS8625
        rootNamespace = new RootNamespaceNode(module);

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

        CustomAttributeArgument arg = new(module.ImportReference(Utils.String), ".NETCoreApp,Version=v7.0");
        CustomAttributeArgument frameworkDisplayName = new(module.ImportReference(Utils.String), ".NET 7.0");
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
        ForeachNamespacesForBuildNestedTypes();
        BuildTypes();

        //foreach (var (definition, builder) in predefinedBuilders)
        //{
        //    if (builder.IsEmpty is false)
        //        module.Types.Add(definition);
        //}
    }

    public void ForeachNamespacesForBuildNestedTypes() => ForeachNamespacesForBuildNestedTypesInternal(rootNamespace);

    public void ForeachNamespacesForBuildNestedTypesInternal(NamespaceNode node)
    {
        foreach (var (_, subNode) in node.SubNamespaces)
        {
            ForeachNamespacesForBuildNestedTypesInternal(subNode);
        }
        foreach (var type in node.Types)
        {
            node.AddType(type);
        }
    }

    public bool TryCreateTypeBuilder(in TypeData typeData, [NotNullWhen(true)] out TypeBuilder? builder)
    {
        builder = null;

        ////not impl
        //if (typeData.Namespaces.Count is not 0)
        //{
        //    Console.WriteLine("QAQ");
        //}

        var type = typeData.Analyzer.CppTypeHandle.RootType;
        switch (type.Type)
        {
            case CppTypeEnum.Class:
            case CppTypeEnum.Struct:
            case CppTypeEnum.Union:

                var definition = new TypeDefinition(string.Empty, typeData.TypeIdentifier, /*TypeAttributes.Public | */TypeAttributes.Class, module.ImportReference(Utils.Object));
                builder = new(definedTypes, null, module, definition, null, 0);
                typeBuilders.Enqueue(builder);
                builders.Add(definition, (typeData, builder));

                InsertTypeIntoNamespaces(typeData, definition);
                definedTypes.Add(typeData.FullTypeIdentifier, definition);

                return true;

            default:
                return false;
        }
    }

    public bool TryCreatePredefinedTypeBuilder(in TypeData typeData, Type predefinedType, [NotNullWhen(true)] out PredefinedTypeExtensionBuilder? builder)
    {
        builder = null;

        var type = typeData.Analyzer.CppTypeHandle.RootType;
        switch (type.Type)
        {
            case CppTypeEnum.Class:
            case CppTypeEnum.Struct:
            case CppTypeEnum.Union:

                var definition = new TypeDefinition("Hosihikari.Minecraft.Extension", $"{predefinedType.Name}EX",
                    TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract, module.ImportReference(Utils.Object));
                builder = new(predefinedType, definedTypes, null, module, definition, null);
                predefinedTypeBuilders.Enqueue(builder);
                predefinedBuilders.Add(definition, builder);

                return true;

            default:
                return false;
        }
    }

    public void CreateBuilderAndAddTypeDefinition(in TypeData typeData, out object? builder)
    {
        builder = null;

        if (definedTypes.ContainsKey(typeData.FullTypeIdentifier) is false)
        {
            var node = typeData.Analyzer.CppTypeHandle.RootType;
            if (TypeReferenceBuilder.TryGetPredefinedType(node, node.Type is CppTypeEnum.Enum, out var predefinedType))
            {
                if (TryCreatePredefinedTypeBuilder(typeData, predefinedType, out var _builder))
                {
                    module.Types.Add(_builder.definition);
                    definedTypes.Add(typeData.FullTypeIdentifier, _builder.definition);
                    builder = _builder;
                }
            }
            else
            {
                if (typeData.Analyzer.CppTypeHandle.RootType.IsTemplate)
                    return;

                if (TryCreateTypeBuilder(typeData, out var _builder))
                {
                    builder = _builder;
                }
            }
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
                    CreateBuilderAndAddTypeDefinition(new TypeData(item.Type), out var _);
                }

                if (item.Params is not null)
                {
                    foreach (var param in item.Params)
                    {
                        CreateBuilderAndAddTypeDefinition(new TypeData(param), out var _);
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
                        CreateBuilderAndAddTypeDefinition(typeData, out var builder);
                        if (builder is TypeBuilder typeBuilder)
                            type = typeBuilder.definition;
                        else if (builder is PredefinedTypeExtensionBuilder predefinedBuilder)
                            type = predefinedBuilder.definition;
                        else
                            continue;
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

                    if (builders.TryGetValue(definition, out var pair))
                    {
                        var (typeData, builder) = pair;

                        builder.SetItems(items);
                        builder.SetVirtualFunctrions(@class.Value.Virtual);
                        builder.SetClassSize(0);
                    }
                    else if (predefinedBuilders.TryGetValue(definition, out var predefinedBuilder))
                    {
                        predefinedBuilder.SetItems(items);
                        predefinedBuilder.SetVirtualFunctrions(@class.Value.Virtual);
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
        while (predefinedTypeBuilders.TryDequeue(out var predefinedBuilder))
        {
            predefinedBuilder.Build();
        }
    }

    public void Wirte()
    {
        assembly.Write(Path.Combine(outputDir, $"{name}.dll"));
    }
}
