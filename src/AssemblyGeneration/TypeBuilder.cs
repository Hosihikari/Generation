using Hosihikari.Utils;

using Mono.Cecil;

using static Hosihikari.Utils.OriginalData.Class;
using static Hosihikari.Generation.AssemblyGeneration.AssemblyBuilder;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Hosihikari.Generation.AssemblyGeneration;

public partial class TypeBuilder
{
    public TypeBuilder(
        Dictionary<string, TypeDefinition> definedTypes,
        List<(ItemAccessType accessType, Item item, int? virtIndex)>? items,
        ModuleDefinition module,
        TypeDefinition definition,
        List<Item>? virtualFunctions,
        ulong? classSize)
    {
        this.definedTypes = definedTypes;
        this.module = module;
        this.definition = definition;
        this.items = items;
        this.virtualFunctions = virtualFunctions;
        this.classSize = classSize;
        fptrFieldNames = new();

        destructorBuilder = new(module);
        interfaceImplBuilder = new(module);
        nativeFunctionPointerBuilder = new(module);
        vftableStructBuilder = new(module);
        methodBuilder = new(module);
    }

    public ModuleDefinition module;
    public TypeDefinition definition;
    public TypeDefinition? originalTypeDefinition;
    public DestructorBuilder.DtorType? dtorType;
    public Dictionary<string, TypeDefinition> definedTypes;
    public List<(ItemAccessType accessType, Item item, int? virtIndex)>? items;
    public List<Item>? virtualFunctions;
    public ulong? classSize;
    public HashSet<string> fptrFieldNames;

    public InterfaceImplBuilder interfaceImplBuilder;
    public NativeFunctionPointerBuilder nativeFunctionPointerBuilder;
    public VftableStructBuilder vftableStructBuilder;
    public MethodBuilder methodBuilder;
    public DestructorBuilder destructorBuilder;


    public void Build()
    {

        interfaceImplBuilder.ImplICppInstanceInterfaceForTypeDefinition(definition, classSize ?? 0);

        var fptrProperties = BuildNativeFunctionPointer(definition, nativeFunctionPointerBuilder);

        var vtable = vftableStructBuilder.BuildVtable(definition, virtualFunctions, definedTypes);
        if (vtable is not null) definition.NestedTypes.Add(vtable);

        BuildNormalMethods(methodBuilder, fptrProperties);

        BuildVirtualMethods(methodBuilder);

        if (dtorType is null)
        {
            var (destructMethod, destructInstanceMethod, dtorType) = destructorBuilder.BuildDtor(
                            DestructorBuilder.DtorType.Empty,
                            default,
                            interfaceImplBuilder.field_Pointer!);
            definition.Methods.Add(destructMethod);
            definition.Methods.Add(destructInstanceMethod);
            this.dtorType = dtorType;
        }
    }

    [MemberNotNull(nameof(originalTypeDefinition))]
    public List<(ItemAccessType accessType, PropertyDefinition property, FunctionPointerType fptrType, Item item, int? virtIndex)>
        BuildNativeFunctionPointer(TypeDefinition definition, NativeFunctionPointerBuilder builder)
    {
        var ret = new List<(ItemAccessType accessType, PropertyDefinition property, FunctionPointerType fptrType, Item item, int? virtIndex)>();

        var originalType = NativeFunctionPointerBuilder.BuildOriginalType(definition);
        definition.NestedTypes.Add(originalType);
        originalTypeDefinition = originalType;

        if (items is not null)
            foreach (var (accessType, item, virtIndex) in items)
            {
                if ((SymbolType)item.SymbolType is SymbolType.StaticField or SymbolType.UnknownFunction)
                    continue;

                try
                {
                    var fptrId = Utils.BuildFptrId(item);
                    var storageType = builder.BuildFptrStorageType(fptrId, item, out var fptrField);

                    originalType.NestedTypes.Add(storageType);

                    var (proeprty, methood, fptrType) = builder.BuildFptrProperty(accessType, definedTypes, fptrFieldNames, item, fptrField);
                    ret.Add((accessType, proeprty, fptrType, item, virtIndex));
                    definition.Properties.Add(proeprty);
                    definition.Methods.Add(methood);
                }
                catch (Exception)
                {
                    continue;
                }
            }

        return ret;
    }

    public void SetItems(List<(ItemAccessType accessType, Item item, int? virtIndex)> items) => this.items = items;

    public void SetVirtualFunctrions(List<Item> items) => virtualFunctions = items;

    public void SetClassSize(ulong size) => classSize = size;

    public void BuildNormalMethods(
        MethodBuilder builder,
        List<(ItemAccessType accessType, PropertyDefinition property, FunctionPointerType fptrType, Item item, int? virtIndex)> properties)
    {
        foreach (var (accessType, property, fptrType, item, virtIndex) in properties)
        {
            var method = builder.BuildMethod(
                accessType,
                property,
                fptrType,
                interfaceImplBuilder!.field_Pointer!,
                interfaceImplBuilder.field_IsOwner!,
                interfaceImplBuilder.field_IsTempStackValue!,
                interfaceImplBuilder.classSize,
                item,
                () =>
                {
                    if (virtIndex is null)
                    {
                        var (destructMethod, destructInstanceMethod, dtorType) = destructorBuilder.BuildDtor(
                            DestructorBuilder.DtorType.Normal,
                            new DestructorBuilder.DtorArgs() { propertyDef = property },
                            interfaceImplBuilder.field_Pointer!);
                        definition.Methods.Add(destructMethod);
                        definition.Methods.Add(destructInstanceMethod);
                        this.dtorType = dtorType;
                    }
                    else
                    {
                        var (destructMethod, destructInstanceMethod, dtorType) = destructorBuilder.BuildDtor(
                            DestructorBuilder.DtorType.Virtual,
                            new DestructorBuilder.DtorArgs() { virtualIndex = virtIndex },
                            interfaceImplBuilder.field_Pointer!);
                        definition.Methods.Add(destructMethod);
                        definition.Methods.Add(destructInstanceMethod);
                        this.dtorType = dtorType;
                    }
                });

            if (method is not null) definition.Methods.Add(method);
        }
    }

    public void BuildVirtualMethods(MethodBuilder builder)
    {
        if (virtualFunctions is null) return;

        for (int i = 0; i < virtualFunctions.Count; i++)
        {
            try
            {
                var fptrType = Utils.BuildFunctionPointerType(module, definedTypes, ItemAccessType.Virtual, virtualFunctions[i]);
                var method = builder.BuildVirtualMethod(fptrType, interfaceImplBuilder.field_Pointer!, i, virtualFunctions[i], () =>
                {
                    var (destructMethod, destructInstanceMethod, dtorType) = destructorBuilder.BuildDtor(
                            DestructorBuilder.DtorType.Virtual,
                            new DestructorBuilder.DtorArgs() { virtualIndex = i },
                            interfaceImplBuilder.field_Pointer!);
                    definition.Methods.Add(destructMethod);
                    definition.Methods.Add(destructInstanceMethod);
                    this.dtorType = dtorType;
                });

                if (method is not null) definition.Methods.Add(method);
            }
            catch (Exception) { continue; }
        }
    }
}
