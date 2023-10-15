global using oc = Mono.Cecil.Cil.OpCodes;

using Hosihikari.Generation.Generator;
using Hosihikari.NativeInterop;
using Hosihikari.NativeInterop.Unmanaged;
using Hosihikari.Utils;
using Mono.Cecil;
using System.Diagnostics.CodeAnalysis;
using System.Text;
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
    private readonly Dictionary<string, TypeDefinition> definedTypes;
    private readonly Queue<(TypeDefinition type, List<(ItemAccessType accessType, Item item)> list)> items;

    private enum ItemAccessType
    {
        PublicStatic = 0,
        PrivateStatic,
        Public,
        Protected,
        Virtual,
    }

    public AssemblyBuilder(AssemblyDefinition assembly, string outputDir, string name)
    {
        this.assembly = assembly;
        module = assembly.MainModule;
        this.outputDir = outputDir;
        definedTypes = new();
        items = new();
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
        BuildFunctionPointer();
    }

    public static bool TryInsertTypeDefinition(in TypeData typeData, ModuleDefinition module, [NotNullWhen(true)] out TypeDefinition? definition)
    {
        definition = null;

        //not impl
        if (typeData.Namespaces.Count is not 0)
            return false;

        var type = typeData.Analyzer.CppTypeHandle.RootType;
        switch (type.Type)
        {
            case CppTypeEnum.Class:
            case CppTypeEnum.Struct:
            case CppTypeEnum.Union:

                var typeDef = new TypeDefinition("Minecraft", typeData.TypeIdentifier, TypeAttributes.Public | TypeAttributes.Class);
                module.Types.Add(typeDef);

                var internalTypeDef = new TypeDefinition(string.Empty, $"I{typeDef.Name}Original", TypeAttributes.NestedPublic | TypeAttributes.Interface);
                typeDef.NestedTypes.Add(internalTypeDef);

                definition = typeDef;
                return true;

            default:
                return false;
        }
    }

    private void ForeachClassesAndBuildTypeDefinition(in OriginalData data)
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
                        if (TryInsertTypeDefinition(typeData, module, out type))
                            definedTypes.Add(typeData.FullTypeIdentifier, type);
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

                var items = new List<(ItemAccessType, Item)>();

                int index = 0;
                foreach (var list in arr)
                {
                    if (list is null) continue;

                    foreach (var item in list)
                    {
                        try
                        {

                            if (string.IsNullOrEmpty(item.Type.Name) is false)
                            {
                                var typeData = new TypeData(item.Type);
                                if (definedTypes.ContainsKey(typeData.FullTypeIdentifier) is false)
                                {
                                    if (TryInsertTypeDefinition(typeData, module, out var type))
                                        definedTypes.Add(typeData.FullTypeIdentifier, type);
                                }
                            }

                            if (item.Params is not null)
                            {
                                foreach (var param in item.Params)
                                {
                                    var typeData = new TypeData(param);
                                    if (definedTypes.ContainsKey(typeData.FullTypeIdentifier) is false)
                                    {
                                        if (TryInsertTypeDefinition(typeData, module, out var type))
                                            definedTypes.Add(typeData.FullTypeIdentifier, type);
                                    }
                                }
                            }

                            items.Add(((ItemAccessType)index, item));
                        }
                        catch (Exception) { continue; }
                    }
                }

                if (definition is not null)
                    this.items.Enqueue((definition, items));
            }
            catch (Exception) { continue; }
        }
    }

    private void BuildFunctionPointer()
    {
        static bool HasThis(ItemAccessType accessType) => (int)accessType > 1;

        while (items.TryDequeue(out var item))
        {
            var type = item.type;
            var list = item.list;

            var originalType = type.NestedTypes.First(t => t.Name == $"I{type.Name}Original");

            foreach (var (accessType, t) in list)
            {
                string fptrId;
                {
                    StringBuilder builder = new();
                    foreach (var c in t.Symbol)
                        builder.Append(TypeAnalyzer.IsLetterOrUnderline(c) ? c : '_');
                    fptrId = builder.ToString();
                }

                var fptrStorageType = new TypeDefinition(string.Empty, $"__FptrStorageType_{fptrId}",
                    TypeAttributes.NestedPrivate | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Class);

                var fptrFieldType = new FunctionPointerType();
                {
                    fptrFieldType.CallingConvention = MethodCallingConvention.Unmanaged;
                    var retType = new TypeData(t.Type);
                }

                var fptrField = new FieldDefinition(
                    $"__Field_{fptrId}", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly, module.ImportReference(typeof(void).MakePointerType()));

                fptrStorageType.Fields.Add(fptrField);

                var cctor = new MethodDefinition(".cctor",
                    MethodAttributes.Static | MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                    module.TypeSystem.Void);

                fptrStorageType.Methods.Add(cctor);

                {
                    var il = cctor.Body.GetILProcessor();
                    il.Append(il.Create(oc.Ldstr, t.Symbol));
                    il.Append(il.Create(oc.Call, module.ImportReference(typeof(SymbolHelper).GetMethod(nameof(SymbolHelper.DlsymPointer)))));
                    il.Append(il.Create(oc.Stsfld, fptrField));
                    il.Append(il.Create(oc.Ret));
                }


                originalType.NestedTypes.Add(fptrStorageType);

                var fptrPropertyDef = new PropertyDefinition($"FunctionPointer_{fptrId}", PropertyAttributes.None, module.ImportReference(typeof(void).MakePointerType()));
                var getMethodDef = new MethodDefinition($"get_FunctionPointer_{fptrId}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Static,
                    module.ImportReference(typeof(void).MakePointerType()));

                {
                    var il = getMethodDef.Body.GetILProcessor();
                    il.Append(il.Create(oc.Ldsfld, fptrField));
                    il.Append(il.Create(oc.Ret));
                }

                originalType.Properties.Add(fptrPropertyDef);
                originalType.Methods.Add(getMethodDef);

                fptrPropertyDef.GetMethod = getMethodDef;

            }


        }

    }

    public void Wirte()
    {
        assembly.Write(Path.Combine(outputDir, $"{name}.dll"));
    }
}
