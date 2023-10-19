using Hosihikari.Generation.Generator;
using Hosihikari.NativeInterop;
using Hosihikari.NativeInterop.Unmanaged;
using Hosihikari.NativeInterop.Unmanaged.Attributes;
using Hosihikari.Utils;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using System;
using System.Text;
using static Hosihikari.Utils.OriginalData.Class;

namespace Hosihikari.Generation.AssemblyGeneration;


/// <summary>
/// ->BuildVtable
///     ->InsertVirtualCppClassAttribute
///     ->BuildVtableLengthProperty
///     ->[foreach]AppendVfunc
///         ->[if failed]AppendUnknownVfunc
/// </summary>
public partial class AssemblyBuilder
{
    private void AppendUnknownVfunc(TypeDefinition definition, int currentIndex, string? funcName = null)
    {
        var fieldDef = new FieldDefinition(funcName is null ? $"__UnknownVirtualFunction_{currentIndex}" : funcName, FieldAttributes.Public | FieldAttributes.InitOnly, module.TypeSystem.IntPtr);
        definition.Fields.Add(fieldDef);
    }

    private void AppendVfunc(TypeDefinition definition, int currentIndex, in Item item)
    {
        try
        {
            var fptrType = BuildFunctionPointerType(ItemAccessType.Virtual, item);
            var fptrName = BuildFptrName(item, new());
            var fieldDef = new FieldDefinition($"vfptr_{fptrName}", FieldAttributes.Public | FieldAttributes.InitOnly, fptrType);
            definition.Fields.Add(fieldDef);
        }
        catch (Exception)
        {
            AppendUnknownVfunc(definition, currentIndex);
        }
    }

    private void InsertVirtualCppClassAttribute(TypeDefinition definition)
    {
        var attribute = new CustomAttribute(module.ImportReference(typeof(VirtualCppClassAttribute).GetConstructors().First()));
        definition.CustomAttributes.Add(attribute);
    }

    private void BuildVtableLengthProperty(TypeDefinition definition, ulong length)
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
            il.Append(il.Create(oc.Ldc_I8, (long)length));
            il.Append(il.Create(oc.Conv_U8));
            il.Append(il.Create(oc.Ret));
        }
        property_VtableLength.GetMethod = getMethod_property_VtableLength;
        definition.Properties.Add(property_VtableLength);
        definition.Methods.Add(getMethod_property_VtableLength);
    }

    private void BuildVtable(TypeDefinition definition, List<Item>? virtualFunctions)
    {
        if (virtualFunctions is null || virtualFunctions.Count is 0)
            return;

        InsertVirtualCppClassAttribute(definition);

        var vtableStructType = new TypeDefinition(
            string.Empty,
            "Vftable", 
            TypeAttributes.NestedPublic |
            TypeAttributes.SequentialLayout, 
            module.ImportReference(typeof(ValueType)));

        BuildVtableLengthProperty(vtableStructType, (ulong)virtualFunctions.Count);

        definition.NestedTypes.Add(vtableStructType);


        for (int i = 0; i < virtualFunctions.Count; i++)
            AppendVfunc(vtableStructType, i, virtualFunctions[i]);
    }
}
