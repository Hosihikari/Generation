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
        propertyMethods = new();

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

    public Dictionary<string, (MethodDefinition? getMethod, MethodDefinition? setMethod)> propertyMethods;

    public bool IsEmpty => items is null && virtualFunctions is null;

    public void Build()
    {
        interfaceImplBuilder.ImplICppInstanceInterfaceForTypeDefinition(definition, classSize ?? 0);

        if (IsEmpty) return;

        var fptrProperties = BuildNativeFunctionPointer(definition, nativeFunctionPointerBuilder);

        var vtable = vftableStructBuilder.BuildVtable(definition, virtualFunctions, definedTypes);
        if (vtable is not null) definition.NestedTypes.Add(vtable);

        BuildNormalMethods(methodBuilder, fptrProperties);

        BuildVirtualMethods(methodBuilder);

        BuildProperties();

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
                    originalType.Properties.Add(proeprty);
                    originalType.Methods.Add(methood);
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
            var isVarArg = fptrType.Parameters.Count > 1 && fptrType.Parameters[^1].ParameterType.FullName == typeof(RuntimeArgumentHandle).FullName;

            var method = builder.BuildMethod(
            accessType,
            property,
            fptrType,
            isVarArg,
            interfaceImplBuilder!.field_Pointer!,
            interfaceImplBuilder.field_IsOwner!,
            interfaceImplBuilder.field_IsTempStackValue!,
            interfaceImplBuilder.classSize,
            item,
            () =>
            {
                var (destructMethod, destructInstanceMethod, dtorType) = destructorBuilder.BuildDtor(
                    DestructorBuilder.DtorType.Normal,
                    new DestructorBuilder.DtorArgs() { propertyDef = property },
                    interfaceImplBuilder.field_Pointer!);
                definition.Methods.Add(destructMethod);
                definition.Methods.Add(destructInstanceMethod);
                this.dtorType = dtorType;
            });

            PlaceMethod(method);
        }
    }

    public void BuildVirtualMethods(MethodBuilder builder)
    {
        if (virtualFunctions is null) return;

        for (int i = 0; i < virtualFunctions.Count; i++)
        {
            try
            {
                var (fptrType, isVarArg) = Utils.BuildFunctionPointerType(module, definedTypes, ItemAccessType.Virtual, virtualFunctions[i]);
                var method = builder.BuildVirtualMethod(fptrType, isVarArg, interfaceImplBuilder.field_Pointer!, i, virtualFunctions[i], () =>
                {
                    var (destructMethod, destructInstanceMethod, dtorType) = destructorBuilder.BuildDtor(
                            DestructorBuilder.DtorType.Virtual,
                            new DestructorBuilder.DtorArgs() { virtualIndex = i },
                            interfaceImplBuilder.field_Pointer!);
                    definition.Methods.Add(destructMethod);
                    definition.Methods.Add(destructInstanceMethod);
                    this.dtorType = dtorType;
                });

                PlaceMethod(method);
            }
            catch (Exception) { continue; }
        }
    }

    public void PlaceMethod(MethodDefinition? method)
    {
        if (method is not null)
        {
            if (Utils.IsPropertyMethod(method, out var tuple))
            {
                if (propertyMethods.TryGetValue(tuple.Value.proeprtyName, out var val))
                {
                    switch (tuple.Value.propertyMethodType)
                    {
                        case Utils.PropertyMethodType.Get:

                            if (val.setMethod is not null &&
                                method.ReturnType.FullName == val.setMethod.Parameters[0].ParameterType.FullName)
                            {
                                method.Name = $"get_{tuple.Value.proeprtyName}";
                                method.Attributes |=
                                    MethodAttributes.Final |
                                    MethodAttributes.HideBySig |
                                    MethodAttributes.SpecialName |
                                    MethodAttributes.NewSlot;

                                propertyMethods[tuple.Value.proeprtyName] = (method, val.setMethod);
                            }
                            else
                            {
                                definition.Methods.Add(method);
                                return;
                            }
                            break;

                        case Utils.PropertyMethodType.Set:

                            if (val.getMethod is not null &&
                                method.Parameters[0].ParameterType.FullName == val.getMethod.ReturnType.FullName)
                            {
                                method.Name = $"set_{tuple.Value.proeprtyName}";
                                method.Attributes |=
                                    MethodAttributes.Final |
                                    MethodAttributes.HideBySig |
                                    MethodAttributes.SpecialName |
                                    MethodAttributes.NewSlot;

                                propertyMethods[tuple.Value.proeprtyName] = (val.getMethod, method);
                            }
                            else
                            {
                                definition.Methods.Add(method);
                                return;
                            }
                            break;
                    }
                }
                else
                {
                    switch (tuple.Value.propertyMethodType)
                    {
                        case Utils.PropertyMethodType.Get:
                            method.Name = $"get_{tuple.Value.proeprtyName}";
                            method.Attributes |=
                                MethodAttributes.Final |
                                MethodAttributes.HideBySig |
                                MethodAttributes.SpecialName |
                                MethodAttributes.NewSlot;

                            propertyMethods.Add(tuple.Value.proeprtyName, (method, null));
                            break;
                        case Utils.PropertyMethodType.Set:
                            method.Name = $"set_{tuple.Value.proeprtyName}";
                            method.Attributes |=
                                MethodAttributes.Final |
                                MethodAttributes.HideBySig |
                                MethodAttributes.SpecialName |
                                MethodAttributes.NewSlot;

                            propertyMethods.Add(tuple.Value.proeprtyName, (null, method));
                            break;
                    }
                }
            }
            else
            {
                definition.Methods.Add(method);
            }
        }
    }

    public void BuildProperties()
    {
        foreach (var (name, (getMethod, setMethod)) in propertyMethods)
        {
            if (getMethod is null && setMethod is null)
                continue;

            var property = new PropertyDefinition(
                name,
                PropertyAttributes.None,
                getMethod is null ?
                    setMethod is null ?
                        throw new NullReferenceException() :
                        setMethod.Parameters[0].ParameterType
                    : getMethod.ReturnType);

            if (getMethod is not null)
            {
                property.GetMethod = getMethod;
                definition.Methods.Add(getMethod);
            }
            if (setMethod is not null)
            {
                property.SetMethod = setMethod;
                definition.Methods.Add(setMethod);
            }
            definition.Properties.Add(property);
        }
    }
}
