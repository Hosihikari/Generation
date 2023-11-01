using Hosihikari.NativeInterop.Unmanaged;
using Hosihikari.NativeInterop.Unmanaged.Attributes;

using Mono.Cecil;

using static Hosihikari.Utils.OriginalData.Class;
using static Hosihikari.Generation.AssemblyGeneration.AssemblyBuilder;

namespace Hosihikari.Generation.AssemblyGeneration;

public class VftableStructBuilder
{
    public ModuleDefinition module;

    public VftableStructBuilder(ModuleDefinition module)
    {
        this.module = module;
    }

    public HashSet<string> vfptrFieldNames = new();

    public void AppendUnknownVfunc(
        TypeDefinition definition,
        int currentIndex,
        string? funcName = null)
    {
        var fieldDef = new FieldDefinition(funcName is null ? $"__UnknownVirtualFunction_{currentIndex}" : funcName, FieldAttributes.Public | FieldAttributes.InitOnly, module.TypeSystem.IntPtr);
        definition.Fields.Add(fieldDef);
    }

    public void AppendVfunc(
        TypeDefinition vfptrStructType,
        Dictionary<string, TypeDefinition> definedTypes,
        int currentIndex,
        in Item item)
    {
        try
        {
            var (fptrType, _) = Utils.BuildFunctionPointerType(module, definedTypes, ItemAccessType.Virtual, item);
            var fptrName = Utils.BuildFptrName(vfptrFieldNames, item, new());
            var fieldDef = new FieldDefinition($"vfptr_{fptrName}", FieldAttributes.Public | FieldAttributes.InitOnly, fptrType);
            vfptrStructType.Fields.Add(fieldDef);
        }
        catch (Exception)
        {
            AppendUnknownVfunc(vfptrStructType, currentIndex);
        }
    }

    public void InsertVirtualCppClassAttribute(TypeDefinition definition)
    {
        var attribute = new CustomAttribute(module.ImportReference(typeof(VirtualCppClassAttribute).GetConstructors().First()));
        definition.CustomAttributes.Add(attribute);
    }

    public void BuildVtableLengthProperty(TypeDefinition definition, ulong length)
    {
        var interfaceImpl = new InterfaceImplementation(module.ImportReference(typeof(ICppVtable)));
        definition.Interfaces.Add(interfaceImpl);
        var property_VtableLength = new PropertyDefinition(
            "VtableLength", PropertyAttributes.None, module.TypeSystem.UInt64);
        var getMethod_property_VtableLength = new MethodDefinition(
            "get_VtableLength",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.Static,
            module.TypeSystem.UInt64);
        getMethod_property_VtableLength.Overrides.Add(module.ImportReference(
            typeof(ICppVtable).GetMethods().First(f => f.Name is "get_VtableLength")));
        {
            var il = getMethod_property_VtableLength.Body.GetILProcessor();
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
            return null;

        InsertVirtualCppClassAttribute(definition);

        var vtableStructType = new TypeDefinition(
            string.Empty,
            "Vftable",
            TypeAttributes.NestedPublic |
            TypeAttributes.SequentialLayout,
            module.ImportReference(typeof(ValueType)));

        BuildVtableLengthProperty(vtableStructType, (ulong)virtualFunctions.Count);

        //definition.NestedTypes.Add(vtableStructType);


        for (int i = 0; i < virtualFunctions.Count; i++)
            AppendVfunc(vtableStructType, definedTypes, i, virtualFunctions[i]);

        return vtableStructType;
    }
}
