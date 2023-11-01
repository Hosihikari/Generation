using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hosihikari.Utils;
using static Hosihikari.Generation.AssemblyGeneration.AssemblyBuilder;
using static Hosihikari.Utils.OriginalData.Class;

namespace Hosihikari.Generation.AssemblyGeneration;

public class PredefinedTypeExtensionBuilder
{
    public Type predefinedType;

    public ModuleDefinition module;
    public TypeDefinition definition;
    public TypeDefinition? originalTypeDefinition;
    public DestructorBuilder.DtorType? dtorType;
    public Dictionary<string, TypeDefinition> definedTypes;
    public List<(ItemAccessType accessType, Item item, int? virtIndex)>? items;
    public List<Item>? virtualFunctions;
    public HashSet<string> fptrFieldNames;

    public NativeFunctionPointerBuilder nativeFunctionPointerBuilder;
    public VftableStructBuilder vftableStructBuilder;
    public MethodBuilder methodBuilder;

    public bool IsEmpty => items is null && virtualFunctions is null;

    public PredefinedTypeExtensionBuilder(
        Type predefinedType,
        Dictionary<string, TypeDefinition> definedTypes,
        List<(ItemAccessType accessType, Item item, int? virtIndex)>? items,
        ModuleDefinition module,
        TypeDefinition definition,
        List<Item>? virtualFunctions)
    {
        this.module = module;
        this.predefinedType = predefinedType;
        this.definedTypes = definedTypes;
        this.module = module;
        this.definition = definition;
        this.items = items;
        this.virtualFunctions = virtualFunctions;
        fptrFieldNames = new();

        nativeFunctionPointerBuilder = new(module);
        vftableStructBuilder = new(module);
        methodBuilder = new(module);
    }

    public void SetItems(List<(ItemAccessType accessType, Item item, int? virtIndex)> items) => this.items = items;

    public void SetVirtualFunctrions(List<Item> items) => virtualFunctions = items;

    public void Build()
    {
        if (IsEmpty) return;

        var fptrProperties = BuildNativeFunctionPointer(definition, nativeFunctionPointerBuilder);

        var vtable = vftableStructBuilder.BuildVtable(definition, virtualFunctions, definedTypes);
        if (vtable is not null) definition.NestedTypes.Add(vtable);

        BuildNormalMethods(methodBuilder, fptrProperties);

        BuildVirtualMethods(methodBuilder);
    }

    [MemberNotNull(nameof(originalTypeDefinition))]
    public List<(ItemAccessType accessType, PropertyDefinition property, FunctionPointerType fptrType, Item item, int? virtIndex)>
        BuildNativeFunctionPointer(TypeDefinition definition, NativeFunctionPointerBuilder builder)
    {
        var ret = new List<(ItemAccessType accessType, PropertyDefinition property, FunctionPointerType fptrType, Item item, int? virtIndex)>();

        var originalType = NativeFunctionPointerBuilder.BuildOriginalType(definition, true);
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

                    var (proeprty, methood, fptrType) = builder.BuildFptrProperty(accessType, definedTypes, fptrFieldNames, item, fptrField, true, predefinedType);
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

    public void PlaceMethod(MethodDefinition? method) { if (method is null) return; definition.Methods.Add(method); }

    public void BuildNormalMethods(
        MethodBuilder builder,
        List<(ItemAccessType accessType, PropertyDefinition property, FunctionPointerType fptrType, Item item, int? virtIndex)> properties)
    {

        foreach (var (accessType, property, fptrType, item, virtIndex) in properties)
        {
            var isVarArg = fptrType.Parameters.Count > 1 && fptrType.Parameters[^1].ParameterType.FullName == typeof(RuntimeArgumentHandle).FullName;

            MethodDefinition? method;
            method = builder.BuildExtensionMethod(
            accessType,
            property,
            fptrType,
            isVarArg,
            item,
            predefinedType,
            () =>
            {
                Item temp = item;
                temp.Name = $"Destructor_{temp.Name[1..]}";
                temp.SymbolType = (int)SymbolType.Function;

                method = builder.BuildExtensionMethod(
                    accessType,
                    property,
                    fptrType,
                    isVarArg,
                    temp,
                    predefinedType,
                    default!);
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
                MethodDefinition? method;
                method = builder.BuildExtensionVirtualMethod(fptrType, isVarArg, predefinedType, i, virtualFunctions[i], () =>
                {
                    Item temp = virtualFunctions[i];
                    temp.Name = $"Destructor_{temp.Name[1..]}";
                    temp.SymbolType = (int)SymbolType.Function;

                    method = builder.BuildExtensionVirtualMethod(fptrType, isVarArg, predefinedType, i, virtualFunctions[i], default!);
                });

                PlaceMethod(method);
            }
            catch (Exception) { continue; }
        }
    }
}
