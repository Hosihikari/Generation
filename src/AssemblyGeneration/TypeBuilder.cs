using Hosihikari.Generation.Utils;
using Mono.Cecil;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using static Hosihikari.Generation.AssemblyGeneration.AssemblyBuilder;
using static Hosihikari.Generation.Utils.OriginalData.Class;

namespace Hosihikari.Generation.AssemblyGeneration;

public class TypeBuilder(
    Dictionary<string, TypeDefinition> types,
    List<(ItemAccessType accessType, Item item, int? virtIndex)>? items,
    ModuleDefinition module,
    TypeDefinition definition,
    List<Item>? functions,
    ulong? size)
{
    public readonly TypeDefinition definition = definition;
    private readonly DestructorBuilder destructorBuilder = new(module);
    private readonly HashSet<string> fptrFieldNames = [];

    private readonly HashSet<string> functionSig = [];

    private readonly InterfaceImplBuilder interfaceImplBuilder = new(module);
    private readonly MethodBuilder methodBuilder = new(module);

    private readonly NativeFunctionPointerBuilder nativeFunctionPointerBuilder = new(module);

    private readonly Dictionary<string, (MethodDefinition? getMethod, MethodDefinition? setMethod)> propertyMethods =
        [];

    private readonly VftableStructBuilder vftableStructBuilder = new(module);

    private DestructorBuilder.DtorType? dtorType;
    public TypeDefinition? originalTypeDefinition;

    private bool IsEmpty => items is null && functions is null;

    public void Build()
    {
        interfaceImplBuilder.ImplICppInstanceInterfaceForTypeDefinition(definition, size ?? 0);

        if (IsEmpty)
        {
            return;
        }

        List<(ItemAccessType accessType, PropertyDefinition property, FunctionPointerType fptrType, Item item, int?
            virtIndex)> fptrProperties = BuildNativeFunctionPointer(definition, nativeFunctionPointerBuilder);

        TypeDefinition? vtable = vftableStructBuilder.BuildVtable(definition, functions, types);
        if (vtable is not null)
        {
            definition.NestedTypes.Add(vtable);
        }

        BuildVirtualMethods(methodBuilder);

        BuildNormalMethods(methodBuilder, fptrProperties);

        BuildProperties();

        if (dtorType is null)
        {
            destructorBuilder.BuildDtor(
                definition,
                DestructorBuilder.DtorType.Empty,
                default,
                interfaceImplBuilder.field_IsOwner!,
                interfaceImplBuilder.field_Pointer!,
                interfaceImplBuilder.field_IsTempStackValue!);
            dtorType = DestructorBuilder.DtorType.Empty;
        }
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
                    MethodDefinition staticMethod) = builder.BuildFptrProperty(accessType, types,
                    fptrFieldNames, item, fptrField);
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

    public void SetItems(List<(ItemAccessType accessType, Item item, int? virtIndex)> items1)
    {
        items = items1;
    }

    public void SetVirtualFunctrions(List<Item> items)
    {
        functions = items;
    }

    public void SetClassSize(ulong size1)
    {
        size = size1;
    }

    private void BuildNormalMethods(
        MethodBuilder builder,
        List<(ItemAccessType accessType, PropertyDefinition property, FunctionPointerType fptrType, Item item, int?
            virtIndex)> properties)
    {
        foreach ((ItemAccessType accessType, PropertyDefinition property, FunctionPointerType fptrType, Item item,
                     _) in properties)
        {
            bool isVarArg = (fptrType.Parameters.Count > 1) && (fptrType.Parameters[^1].ParameterType.FullName ==
                                                                typeof(RuntimeArgumentHandle).FullName);

            MethodDefinition? method = builder.BuildMethod(
                accessType,
                property,
                fptrType,
                isVarArg,
                interfaceImplBuilder.field_Pointer!,
                interfaceImplBuilder.field_IsOwner!,
                interfaceImplBuilder.field_IsTempStackValue!,
                interfaceImplBuilder.classSize,
                item,
                () =>
                {
                    if (dtorType is not null)
                        return;

                    destructorBuilder.BuildDtor(
                        definition,
                        DestructorBuilder.DtorType.Normal,
                        new() { propertyDef = property },
                        interfaceImplBuilder.field_IsOwner!,
                        interfaceImplBuilder.field_Pointer!,
                        interfaceImplBuilder.field_IsTempStackValue!);
                    dtorType = DestructorBuilder.DtorType.Normal;
                });

            PlaceMethod(method);
        }
    }

    private void BuildVirtualMethods(MethodBuilder builder)
    {
        if (functions is null)
        {
            return;
        }

        for (int i = 0; i < functions.Count; i++)
        {
            try
            {
                (FunctionPointerType fptrType, bool isVarArg) = Utils.BuildFunctionPointerType(module, types,
                    ItemAccessType.Virtual, functions[i]);
                int i1 = i;
                MethodDefinition? method = builder.BuildVirtualMethod(fptrType, isVarArg,
                    interfaceImplBuilder.field_Pointer!, i, functions[i], () =>
                    {
                        if (dtorType is not null)
                            return;

                        destructorBuilder.BuildDtor(
                            definition,
                            DestructorBuilder.DtorType.Virtual,
                            new() { virtualIndex = i1 },
                            interfaceImplBuilder.field_IsOwner!,
                            interfaceImplBuilder.field_Pointer!,
                            interfaceImplBuilder.field_IsTempStackValue!);

                        dtorType = DestructorBuilder.DtorType.Virtual;
                    });

                PlaceMethod(method);
            }
            catch
            {
                // ignored
            }
        }
    }

    private void PlaceMethod(MethodDefinition? method)
    {
        if (method is null)
        {
            return;
        }

        StringBuilder builder = new(method.Name);
        foreach (ParameterDefinition? param in method.Parameters)
        {
            builder.Append('+').Append(param.ParameterType);
        }

        string sig = builder.ToString();

        if (functionSig.Add(sig) is false)
        {
            return;
        }

        if (Utils.IsPropertyMethod(method,
                out (Utils.PropertyMethodType propertyMethodType, string proeprtyName)? tuple))
        {
            if (propertyMethods.TryGetValue(tuple.Value.proeprtyName,
                    out (MethodDefinition? getMethod, MethodDefinition? setMethod) val))
            {
                switch (tuple.Value.propertyMethodType)
                {
                    case Utils.PropertyMethodType.Get:

                        if (val.setMethod is not null &&
                            (method.ReturnType.FullName == val.setMethod.Parameters[0].ParameterType.FullName))
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
                        }

                        break;

                    case Utils.PropertyMethodType.Set:

                        if (val.getMethod is not null &&
                            (method.Parameters[0].ParameterType.FullName == val.getMethod.ReturnType.FullName))
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
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
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
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        else
        {
            definition.Methods.Add(method);
        }
    }

    private void BuildProperties()
    {
        foreach ((string name, (MethodDefinition? getMethod, MethodDefinition? setMethod)) in propertyMethods)
        {
            if (getMethod is null && setMethod is null)
            {
                continue;
            }

            PropertyDefinition property = new(
                name,
                PropertyAttributes.None,
                getMethod is null
                    ? setMethod is null ? throw new NullReferenceException() : setMethod.Parameters[0].ParameterType
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