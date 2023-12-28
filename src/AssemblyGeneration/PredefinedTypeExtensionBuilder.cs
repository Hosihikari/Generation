using System.Diagnostics.CodeAnalysis;
using Hosihikari.Generation.Utils;
using Mono.Cecil;
using static Hosihikari.Generation.AssemblyGeneration.AssemblyBuilder;
using static Hosihikari.Generation.Utils.OriginalData.Class;

namespace Hosihikari.Generation.AssemblyGeneration;

public class PredefinedTypeExtensionBuilder
{
    private readonly Dictionary<string, TypeDefinition> definedTypes;
    public readonly TypeDefinition definition;
    private readonly HashSet<string> fptrFieldNames;
    private readonly MethodBuilder methodBuilder;

    private readonly ModuleDefinition module;

    private readonly NativeFunctionPointerBuilder nativeFunctionPointerBuilder;
    private readonly Type predefinedType;
    private readonly VftableStructBuilder vftableStructBuilder;
    private List<(ItemAccessType accessType, Item item, int? virtIndex)>? items;
    public TypeDefinition? originalTypeDefinition;
    private List<Item>? virtualFunctions;

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
        fptrFieldNames = [];

        nativeFunctionPointerBuilder = new(module);
        vftableStructBuilder = new(module);
        methodBuilder = new(module);
    }

    private bool IsEmpty => items is null && virtualFunctions is null;

    public void SetItems(List<(ItemAccessType accessType, Item item, int? virtIndex)> items)
    {
        this.items = items;
    }

    public void SetVirtualFunctrions(List<Item> items)
    {
        virtualFunctions = items;
    }

    public void Build()
    {
        if (IsEmpty)
        {
            return;
        }

        List<(ItemAccessType accessType, PropertyDefinition property, FunctionPointerType fptrType, Item item, int?
            virtIndex)> fptrProperties = BuildNativeFunctionPointer(definition, nativeFunctionPointerBuilder);

        TypeDefinition? vtable = vftableStructBuilder.BuildVtable(definition, virtualFunctions, definedTypes);
        if (vtable is not null)
        {
            definition.NestedTypes.Add(vtable);
        }

        BuildNormalMethods(methodBuilder, fptrProperties);

        BuildVirtualMethods(methodBuilder);
    }

    [MemberNotNull(nameof(originalTypeDefinition))]
    private List<(ItemAccessType accessType, PropertyDefinition property, FunctionPointerType fptrType, Item item, int?
            virtIndex)>
        BuildNativeFunctionPointer(TypeDefinition definition, NativeFunctionPointerBuilder builder)
    {
        List<(ItemAccessType accessType, PropertyDefinition property, FunctionPointerType fptrType, Item item, int?
            virtIndex)> ret = [];

        TypeDefinition originalType = NativeFunctionPointerBuilder.BuildOriginalType();
        definition.NestedTypes.Add(originalType);
        originalTypeDefinition = originalType;

        if (items is null)
        {
            return ret;
        }

        foreach ((ItemAccessType accessType, Item item, int? virtIndex) in items)
        {
            if ((SymbolType)item.SymbolType is SymbolType.StaticField or SymbolType.UnknownFunction)
            {
                continue;
            }

            try
            {
                string fptrId = Utils.BuildFptrId(item);
                TypeDefinition storageType =
                    builder.BuildFptrStorageType(fptrId, item, out FieldDefinition fptrField);

                originalType.NestedTypes.Add(storageType);

                (PropertyDefinition proeprty, MethodDefinition method, FunctionPointerType fptrType,
                    MethodDefinition staticMethod) = builder.BuildFptrProperty(accessType, definedTypes,
                    fptrFieldNames, item, fptrField, true, predefinedType);
                ret.Add((accessType, proeprty, fptrType, item, virtIndex));
                originalType.Properties.Add(proeprty);
                originalType.Methods.Add(method);
                originalType.Methods.Add(staticMethod);
            }
            catch
            {
                // ignored
            }
        }

        return ret;
    }

    private void PlaceMethod(MethodDefinition? method)
    {
        if (method is null)
        {
            return;
        }

        definition.Methods.Add(method);
    }

    private void BuildNormalMethods(
        MethodBuilder builder,
        List<(ItemAccessType accessType, PropertyDefinition property, FunctionPointerType fptrType, Item item, int?
            virtIndex)> properties)
    {
        foreach ((ItemAccessType accessType, PropertyDefinition property, FunctionPointerType fptrType, Item item,
                     int? _) in properties)
        {
            bool isVarArg = fptrType.Parameters.Count > 1 && fptrType.Parameters[^1].ParameterType.FullName ==
                typeof(RuntimeArgumentHandle).FullName;

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

    private void BuildVirtualMethods(MethodBuilder builder)
    {
        if (virtualFunctions is null)
        {
            return;
        }

        for (int i = 0; i < virtualFunctions.Count; i++)
        {
            (FunctionPointerType fptrType, bool isVarArg) = Utils.BuildFunctionPointerType(module, definedTypes,
                ItemAccessType.Virtual, virtualFunctions[i]);
            MethodDefinition? method;
            int i1 = i;
            method = builder.BuildExtensionVirtualMethod(fptrType, isVarArg, predefinedType, i, virtualFunctions[i],
                () =>
                {
                    Item temp = virtualFunctions[i1];
                    temp.Name = $"Destructor_{temp.Name[1..]}";
                    temp.SymbolType = (int)SymbolType.Function;

                    method = builder.BuildExtensionVirtualMethod(fptrType, isVarArg, predefinedType, i1,
                        virtualFunctions[i1], default!);
                });

            PlaceMethod(method);
        }
    }
}