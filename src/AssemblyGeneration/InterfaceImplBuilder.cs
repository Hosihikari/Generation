using Hosihikari.NativeInterop.Unmanaged;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Diagnostics.CodeAnalysis;

namespace Hosihikari.Generation.AssemblyGeneration;

public class InterfaceImplBuilder(ModuleDefinition module)
{
    private const string PointerStorageFieldName = "__pointer_storage";
    private const string IsOwnerStorageFieldName = "__isOwner_storage";
    private const string IsTempStackValueStorageFieldName = "__isTempStackValue_storage";

    public ulong classSize;

    public MethodDefinition? ctor_Default;
    public MethodDefinition? ctor_Ptr_Owns;
    public FieldDefinition? field_IsOwner;
    public FieldDefinition? field_IsTempStackValue;
    public FieldDefinition? field_Pointer;
    public MethodDefinition? method_ConstructInstance;

    public MethodDefinition? method_ConstructInstance_object;

    public MethodDefinition? method_op_Implicit_nint;
    public MethodDefinition? method_op_Implicit_voidPtr;

    public PropertyDefinition? property_ClassSize;
    public MethodDefinition? property_ClassSize_getMethod;

    public PropertyDefinition? property_IsOwner;
    public MethodDefinition? property_IsOwner_getMethod;
    public MethodDefinition? property_IsOwner_setMethod;

    public PropertyDefinition? property_IsTempStackValue;
    public MethodDefinition? property_IsTempStackValue_getMethod;
    public MethodDefinition? property_IsTempStackValue_setMethod;

    public PropertyDefinition? property_Pointer;
    public MethodDefinition? property_Pointer_getMethod;
    public MethodDefinition? property_Pointer_setMethod;

    [MemberNotNull(nameof(ctor_Default), nameof(ctor_Ptr_Owns), nameof(method_ConstructInstance_object),
        nameof(method_ConstructInstance))]
    [MemberNotNull(
        nameof(property_Pointer),
        nameof(field_Pointer),
        nameof(property_Pointer_setMethod),
        nameof(property_Pointer_getMethod))]
    [MemberNotNull(
        nameof(property_IsOwner),
        nameof(field_IsOwner),
        nameof(property_IsOwner_getMethod),
        nameof(property_IsOwner_setMethod))]
    [MemberNotNull(
        nameof(property_IsTempStackValue),
        nameof(field_IsTempStackValue),
        nameof(property_IsTempStackValue_getMethod),
        nameof(property_IsTempStackValue_setMethod))]
    [MemberNotNull(nameof(method_op_Implicit_nint), nameof(method_op_Implicit_voidPtr))]
    [MemberNotNull(nameof(property_ClassSize), nameof(property_ClassSize_getMethod))]
    public void ImplICppInstanceInterfaceForTypeDefinition(
        TypeDefinition definition,
        ulong classSize = 0)
    {
        if (definition.IsClass is false)
        {
            throw new InvalidOperationException();
        }

        GenericInstanceType IcppInstanceType = new(module.ImportReference(typeof(ICppInstance<>)));
        IcppInstanceType.GenericArguments.Add(definition);

        definition.Interfaces.Add(new(IcppInstanceType));
        definition.Interfaces.Add(new(module.ImportReference(typeof(ICppInstanceNonGeneric))));

        BuildPointerProperty(definition, out FieldDefinition ptr);
        BuildIsOwnerProperty(definition, out FieldDefinition owns);
        BuildIsTempStackValueProperty(definition, out FieldDefinition isTempStackValue);
        BuildDefaultCtor(definition, ptr, owns, isTempStackValue);
        BuildImplicitOperator(definition);
        BuildClassSizeProperty(definition, classSize);
    }

    [MemberNotNull(nameof(ctor_Default), nameof(ctor_Ptr_Owns), nameof(method_ConstructInstance_object),
        nameof(method_ConstructInstance))]
    private void BuildDefaultCtor(
        TypeDefinition definition,
        FieldReference ptr,
        FieldReference owns,
        FieldReference isTempStackValue)
    {
        MethodDefinition ctorDefault = new(
            ".ctor",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName,
            module.ImportReference(typeof(void)));
        {
            ILProcessor? il = ctorDefault.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Call, module.ImportReference(Utils.Object.GetConstructors().First()));
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldc_I8, 0L);
            il.Emit(OC.Conv_Ovf_I);
            il.Emit(OC.Stfld, ptr);
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldc_I4_0);
            il.Emit(OC.Stfld, owns);
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldc_I4_0);
            il.Emit(OC.Stfld, isTempStackValue);
            il.Emit(OC.Ret);
        }

        MethodDefinition ctor = new(
            ".ctor",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName,
            module.ImportReference(typeof(void)));
        ParameterDefinition ptrParameter = new("ptr", ParameterAttributes.None, module.ImportReference(typeof(nint)));
        ParameterDefinition ownsParameter =
            new("owns", ParameterAttributes.Optional, module.ImportReference(typeof(bool)))
                { Constant = false };
        ParameterDefinition isTempStackValueParameter =
            new("isTempStackValue", ParameterAttributes.Optional,
                module.ImportReference(typeof(bool))) { Constant = true };
        ctor.Parameters.Add(ptrParameter);
        ctor.Parameters.Add(ownsParameter);
        ctor.Parameters.Add(isTempStackValueParameter);
        {
            ILProcessor? il = ctor.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Call, module.ImportReference(Utils.Object.GetConstructors().First()));
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldarg_1);
            il.Emit(OC.Stfld, ptr);
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldarg_2);
            il.Emit(OC.Stfld, owns);
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldarg_3);
            il.Emit(OC.Stfld, isTempStackValue);
            il.Emit(OC.Ret);
        }

        GenericInstanceType IcppInstanceType = new(module.ImportReference(typeof(ICppInstance<>)));
        IcppInstanceType.GenericArguments.Add(definition);

        MethodReference methodReference = new("ConstructInstance",
            IcppInstanceType.Resolve().GenericParameters[0], IcppInstanceType);
        methodReference.Parameters.Add(new(module.ImportReference(typeof(nint))));
        methodReference.Parameters.Add(new(module.ImportReference(typeof(bool))));
        methodReference.Parameters.Add(new(module.ImportReference(typeof(bool))));

        MethodDefinition ConstructInstanceMethod = new(
            "ConstructInstance",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.Static,
            definition);

        ConstructInstanceMethod.Overrides.Add(module.ImportReference(methodReference));
        ConstructInstanceMethod.Parameters.Add(new("ptr", ParameterAttributes.None,
            module.ImportReference(typeof(nint))));
        ConstructInstanceMethod.Parameters.Add(new("owns", ParameterAttributes.None,
            module.ImportReference(typeof(bool))));
        ConstructInstanceMethod.Parameters.Add(new("isTempStackValue", ParameterAttributes.None,
            module.ImportReference(typeof(bool))));
        {
            ILProcessor? il = ConstructInstanceMethod.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldarg_1);
            il.Emit(OC.Ldarg_2);
            il.Emit(OC.Newobj, ctor);
            il.Emit(OC.Ret);
        }

        MethodDefinition ConstructInstanceNonGenericMethod = new(
            $"{typeof(ICppInstanceNonGeneric).FullName}.ConstructInstance",
            MethodAttributes.Private |
            MethodAttributes.HideBySig |
            MethodAttributes.Static,
            module.ImportReference(Utils.Object));
        ConstructInstanceNonGenericMethod.Overrides.Add(module.ImportReference(
            typeof(ICppInstanceNonGeneric)
                .GetMethods()
                .First(f => f.Name == nameof(ICppInstanceNonGeneric.ConstructInstance))));

        ConstructInstanceNonGenericMethod.Parameters.Add(new("ptr", ParameterAttributes.None,
            module.ImportReference(typeof(nint))));
        ConstructInstanceNonGenericMethod.Parameters.Add(new("owns", ParameterAttributes.None,
            module.ImportReference(typeof(bool))));
        ConstructInstanceNonGenericMethod.Parameters.Add(new("isTempStackValue", ParameterAttributes.None,
            module.ImportReference(typeof(bool))));
        {
            ILProcessor? il = ConstructInstanceNonGenericMethod.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldarg_1);
            il.Emit(OC.Ldarg_2);
            il.Emit(OC.Call, ConstructInstanceMethod);
            il.Emit(OC.Ret);
        }

        definition.Methods.Add(ctor);
        definition.Methods.Add(ctorDefault);
        definition.Methods.Add(ConstructInstanceMethod);
        definition.Methods.Add(ConstructInstanceNonGenericMethod);

        ctor_Default = ctorDefault;
        ctor_Ptr_Owns = ctor;
        method_ConstructInstance = ConstructInstanceMethod;
        method_ConstructInstance_object = ConstructInstanceNonGenericMethod;
    }

    [MemberNotNull(
        nameof(property_Pointer),
        nameof(field_Pointer),
        nameof(property_Pointer_setMethod),
        nameof(property_Pointer_getMethod))]
    private void BuildPointerProperty(TypeDefinition definition, out FieldDefinition ptr)
    {
        FieldDefinition pointerField = new(
            PointerStorageFieldName, FieldAttributes.Private, module.ImportReference(typeof(nint)));

        PropertyDefinition pointerProperty = new(
            "Pointer", PropertyAttributes.None, module.ImportReference(typeof(nint)));

        MethodDefinition pointerProperty_getMethod = new(
            "get_Pointer",
            MethodAttributes.Public |
            MethodAttributes.Final |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.NewSlot |
            MethodAttributes.Virtual,
            module.ImportReference(typeof(nint)));
        {
            ILProcessor? il = pointerProperty_getMethod.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldfld, pointerField);
            il.Emit(OC.Ret);
        }
        MethodDefinition pointerProperty_setMethod = new(
            "set_Pointer",
            MethodAttributes.Public |
            MethodAttributes.Final |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.NewSlot |
            MethodAttributes.Virtual,
            module.ImportReference(typeof(void)));
        pointerProperty_setMethod.Parameters.Add(new("value", ParameterAttributes.None,
            module.ImportReference(typeof(nint))));
        {
            ILProcessor? il = pointerProperty_setMethod.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldarg_1);
            il.Emit(OC.Stfld, pointerField);
            il.Emit(OC.Ret);
        }

        pointerProperty.GetMethod = pointerProperty_getMethod;
        pointerProperty.SetMethod = pointerProperty_setMethod;

        definition.Properties.Add(pointerProperty);
        definition.Methods.Add(pointerProperty_getMethod);
        definition.Methods.Add(pointerProperty_setMethod);
        definition.Fields.Add(pointerField);
        ptr = pointerField;

        property_Pointer = pointerProperty;
        property_Pointer_getMethod = pointerProperty_getMethod;
        property_Pointer_setMethod = pointerProperty_setMethod;
        field_Pointer = pointerField;
    }

    [MemberNotNull(
        nameof(property_IsOwner),
        nameof(field_IsOwner),
        nameof(property_IsOwner_getMethod),
        nameof(property_IsOwner_setMethod))]
    private void BuildIsOwnerProperty(TypeDefinition definition, out FieldDefinition owns)
    {
        FieldDefinition isOwnerField = new(
            IsOwnerStorageFieldName, FieldAttributes.Private, module.ImportReference(typeof(bool)));

        PropertyDefinition isOwnerProperty = new(
            "IsOwner", PropertyAttributes.None, module.ImportReference(typeof(bool)));

        MethodDefinition isOwnerProperty_getMethod = new(
            "get_IsOwner",
            MethodAttributes.Public |
            MethodAttributes.Final |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.NewSlot |
            MethodAttributes.Virtual,
            module.ImportReference(typeof(bool)));
        {
            ILProcessor? il = isOwnerProperty_getMethod.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldfld, isOwnerField);
            il.Emit(OC.Ret);
        }
        MethodDefinition isOwnerProperty_setMethod = new(
            "set_IsOwner",
            MethodAttributes.Public |
            MethodAttributes.Final |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.NewSlot |
            MethodAttributes.Virtual,
            module.ImportReference(typeof(void)));
        isOwnerProperty_setMethod.Parameters.Add(new("value", ParameterAttributes.None,
            module.ImportReference(typeof(bool))));
        {
            ILProcessor? il = isOwnerProperty_setMethod.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldarg_1);
            il.Emit(OC.Stfld, isOwnerField);
            il.Emit(OC.Ret);
        }

        isOwnerProperty.GetMethod = isOwnerProperty_getMethod;
        isOwnerProperty.SetMethod = isOwnerProperty_setMethod;

        definition.Properties.Add(isOwnerProperty);
        definition.Methods.Add(isOwnerProperty_getMethod);
        definition.Methods.Add(isOwnerProperty_setMethod);
        definition.Fields.Add(isOwnerField);
        owns = isOwnerField;

        property_IsOwner = isOwnerProperty;
        property_IsOwner_getMethod = isOwnerProperty_getMethod;
        property_IsOwner_setMethod = isOwnerProperty_setMethod;
        field_IsOwner = isOwnerField;
    }

    [MemberNotNull(
        nameof(property_IsTempStackValue),
        nameof(field_IsTempStackValue),
        nameof(property_IsTempStackValue_getMethod),
        nameof(property_IsTempStackValue_setMethod))]
    private void BuildIsTempStackValueProperty(TypeDefinition definition, out FieldDefinition isTempStackValue)
    {
        FieldDefinition isTempStackValueField = new(
            IsTempStackValueStorageFieldName, FieldAttributes.Private, module.ImportReference(typeof(bool)));

        PropertyDefinition isTempStackValueProperty = new(
            "IsTempStackValue", PropertyAttributes.None, module.ImportReference(typeof(bool)));

        MethodDefinition isTempStackValueProperty_getMethod = new(
            "get_IsTempStackValue",
            MethodAttributes.Public |
            MethodAttributes.Final |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.NewSlot |
            MethodAttributes.Virtual,
            module.ImportReference(typeof(bool)));
        {
            ILProcessor? il = isTempStackValueProperty_getMethod.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldfld, isTempStackValueField);
            il.Emit(OC.Ret);
        }
        MethodDefinition isTempStackValueProperty_setMethod = new(
            "set_IsTempStackValue",
            MethodAttributes.Public |
            MethodAttributes.Final |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.NewSlot |
            MethodAttributes.Virtual,
            module.ImportReference(typeof(void)));
        isTempStackValueProperty_setMethod.Parameters.Add(new("value", ParameterAttributes.None,
            module.ImportReference(typeof(bool))));
        {
            ILProcessor? il = isTempStackValueProperty_setMethod.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldarg_1);
            il.Emit(OC.Stfld, isTempStackValueField);
            il.Emit(OC.Ret);
        }

        isTempStackValueProperty.GetMethod = isTempStackValueProperty_getMethod;
        isTempStackValueProperty.SetMethod = isTempStackValueProperty_setMethod;

        definition.Properties.Add(isTempStackValueProperty);
        definition.Methods.Add(isTempStackValueProperty_getMethod);
        definition.Methods.Add(isTempStackValueProperty_setMethod);
        definition.Fields.Add(isTempStackValueField);
        isTempStackValue = isTempStackValueField;

        property_IsTempStackValue = isTempStackValueProperty;
        property_IsTempStackValue_getMethod = isTempStackValueProperty_getMethod;
        property_IsTempStackValue_setMethod = isTempStackValueProperty_setMethod;
        field_IsTempStackValue = isTempStackValueField;
    }


    [MemberNotNull(nameof(method_op_Implicit_nint), nameof(method_op_Implicit_voidPtr))]
    private void BuildImplicitOperator(TypeDefinition definition)
    {
        GenericInstanceType IcppInstanceType = new(module.ImportReference(typeof(ICppInstance<>)));
        IcppInstanceType.GenericArguments.Add(definition);

        MethodDefinition op_2_nint = new(
            "op_Implicit",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.Static,
            module.ImportReference(typeof(nint)));
        op_2_nint.Parameters.Add(new("ins", ParameterAttributes.None, definition));


        op_2_nint.Overrides.Add(
            new("op_Implicit", module.ImportReference(typeof(nint)), IcppInstanceType)
            {
                Parameters = { new(IcppInstanceType.Resolve().GenericParameters[0]) }
            });
        {
            ILProcessor? il = op_2_nint.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            FieldDefinition? field = definition.Fields.First(f => f.Name is PointerStorageFieldName);
            il.Emit(OC.Ldfld, field);
            il.Emit(OC.Ret);
        }


        MethodDefinition op_2_voidPtr = new(
            "op_Implicit",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.Static,
            module.ImportReference(typeof(void).MakePointerType()));
        op_2_voidPtr.Parameters.Add(new("ins", ParameterAttributes.None, definition));


        op_2_voidPtr.Overrides.Add(
            new("op_Implicit", module.ImportReference(typeof(void).MakePointerType()), IcppInstanceType)
            {
                Parameters = { new(IcppInstanceType.Resolve().GenericParameters[0]) }
            });
        {
            ILProcessor? il = op_2_voidPtr.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            FieldDefinition? field = definition.Fields.First(f => f.Name is PointerStorageFieldName);
            il.Emit(OC.Ldflda, field);
            il.Emit(OC.Call, module.ImportReference(typeof(nint).GetMethod(nameof(nint.ToPointer))));
            il.Emit(OC.Ret);
        }

        definition.Methods.Add(op_2_nint);
        definition.Methods.Add(op_2_voidPtr);

        method_op_Implicit_nint = op_2_nint;
        method_op_Implicit_voidPtr = op_2_voidPtr;
    }

    [MemberNotNull(nameof(property_ClassSize), nameof(property_ClassSize_getMethod))]
    private void BuildClassSizeProperty(TypeDefinition definition, ulong classSize)
    {
        PropertyDefinition property = new(
            "ClassSize", PropertyAttributes.None, module.ImportReference(typeof(ulong)));

        MethodDefinition getMethod = new(
            "get_ClassSize",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.Static,
            module.ImportReference(typeof(ulong)));
        getMethod.Overrides.Add(module.ImportReference(
            typeof(ICppInstanceNonGeneric).GetMethods().First(f => f.Name is "get_ClassSize")));
        {
            ILProcessor? il = getMethod.Body.GetILProcessor();
            il.Emit(OC.Ldc_I8, (long)classSize);
            il.Emit(OC.Conv_U8);
            il.Emit(OC.Ret);
        }

        property.GetMethod = getMethod;

        definition.Properties.Add(property);
        definition.Methods.Add(getMethod);

        property_ClassSize = property;
        property_ClassSize_getMethod = getMethod;

        this.classSize = classSize;
    }
}