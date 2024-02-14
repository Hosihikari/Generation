using Hosihikari.NativeInterop.Unmanaged;
using Hosihikari.NativeInterop.Unmanaged.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using static Hosihikari.Generation.AssemblyGeneration.AssemblyBuilder;
using static Hosihikari.Generation.Utils.OriginalData.Class;

namespace Hosihikari.Generation.AssemblyGeneration;

public class VftableStructBuilder
{
    private readonly ModuleDefinition module;

    private readonly HashSet<string> vfptrFieldNames = [];

    public VftableStructBuilder(ModuleDefinition module)
    {
        this.module = module;
    }

    private void AppendUnknownVfunc(
        TypeDefinition definition,
        int currentIndex,
        string? funcName = null)
    {
        FieldDefinition fieldDef =
            new(funcName ?? $"__UnknownVirtualFunction_{currentIndex}",
                FieldAttributes.Public | FieldAttributes.InitOnly, module.ImportReference(typeof(nint)));
        definition.Fields.Add(fieldDef);
    }

    private void AppendVfunc(
        TypeDefinition vfptrStructType,
        Dictionary<string, TypeDefinition> definedTypes,
        int currentIndex,
        in Item item)
    {
        try
        {
            (FunctionPointerType fptrType, _) =
                Utils.BuildFunctionPointerType(module, definedTypes, ItemAccessType.Virtual, item);
            string fptrName = Utils.BuildFptrName(vfptrFieldNames, item);
            FieldDefinition fieldDef = new($"vfptr_{fptrName}",
                FieldAttributes.Public | FieldAttributes.InitOnly, fptrType);
            vfptrStructType.Fields.Add(fieldDef);
        }
        catch
        {
            AppendUnknownVfunc(vfptrStructType, currentIndex);
        }
    }

    private void InsertVirtualCppClassAttribute(ICustomAttributeProvider definition)
    {
        CustomAttribute attribute =
            new(module.ImportReference(typeof(VirtualCppClassAttribute).GetConstructors().First()));
        definition.CustomAttributes.Add(attribute);
    }

    private void BuildVtableLengthProperty(TypeDefinition definition, ulong length)
    {
        InterfaceImplementation interfaceImpl = new(module.ImportReference(typeof(ICppVtable)));
        definition.Interfaces.Add(interfaceImpl);
        PropertyDefinition property_VtableLength = new(
            "VtableLength", PropertyAttributes.None, module.ImportReference(typeof(ulong)));
        MethodDefinition getMethod_property_VtableLength = new(
            "get_VtableLength",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.Static,
            module.ImportReference(typeof(ulong)));
        getMethod_property_VtableLength.Overrides.Add(module.ImportReference(
            typeof(ICppVtable).GetMethods().First(f => f.Name is "get_VtableLength")));
        {
            ILProcessor? il = getMethod_property_VtableLength.Body.GetILProcessor();
            il.Emit(OC.Ldc_I8, (long)length);
            il.Emit(OC.Conv_U8);
            il.Emit(OC.Ret);
        }
        property_VtableLength.GetMethod = getMethod_property_VtableLength;
        definition.Properties.Add(property_VtableLength);
        definition.Methods.Add(getMethod_property_VtableLength);
    }

    public TypeDefinition? BuildVtable(
        TypeDefinition definition,
        List<Item>? virtualFunctions,
        Dictionary<string, TypeDefinition> definedTypes)
    {
        if (virtualFunctions is null || virtualFunctions.Count is 0)
        {
            return null;
        }

        InsertVirtualCppClassAttribute(definition);

        TypeDefinition vtableStructType = new(
            string.Empty,
            "Vftable",
            TypeAttributes.NestedPublic |
            TypeAttributes.Sealed |
            TypeAttributes.AnsiClass |
            TypeAttributes.BeforeFieldInit |
            TypeAttributes.SequentialLayout,
            module.ImportReference(Utils.ValueType));

        BuildVtableLengthProperty(vtableStructType, (ulong)virtualFunctions.Count);


        for (int i = 0; i < virtualFunctions.Count; i++)
        {
            AppendVfunc(vtableStructType, definedTypes, i, virtualFunctions[i]);
        }

        return vtableStructType;
    }
}