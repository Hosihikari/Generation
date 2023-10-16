using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Hosihikari.NativeInterop.Unmanaged;
using Mono.Cecil;
using static Hosihikari.Utils.OriginalData.Class;

namespace Hosihikari.Generation.AssemblyGeneration
{

    public partial class AssemblyBuilder
    {
        private const string PointerStorageFieldName = "__pointer_storage";
        private const string IsOwnerStorageFieldName = "__isOwner_storage";

        private MethodDefinition? ctor_Default;
        private MethodDefinition? ctor_Ptr_Owns;

        private PropertyDefinition? property_Pointer;
        private FieldDefinition? field_Pointer;
        private MethodDefinition? property_Pointer_setMethod;
        private MethodDefinition? property_Pointer_getMethod;

        private PropertyDefinition? property_IsOwner;
        private FieldDefinition? field_IsOwner;
        private MethodDefinition? property_IsOwner_getMethod;
        private MethodDefinition? property_IsOwner_setMethod;

        private PropertyDefinition? property_ClassSize;
        private MethodDefinition? property_ClassSize_getMethod;

        private MethodDefinition? method_Destruct;
        private MethodDefinition? method_DestructInstance;

        private MethodDefinition? method_ConstructInstance_object;
        private MethodDefinition? method_ConstructInstance;

        private MethodDefinition? method_op_Implicit_nint;
        private MethodDefinition? method_op_Implicit_voidPtr;



        public void ImplICppInstanceInterfaceForTypeDefinition(TypeDefinition definition, in Item? dtor = null, ulong classSize = 0)
        {
            if (definition.IsClass is false)
                throw new InvalidOperationException();

            var IcppInstanceType = new GenericInstanceType(module.ImportReference(typeof(ICppInstance<>)));
            IcppInstanceType.GenericArguments.Add(definition);

            definition.Interfaces.Add(new(IcppInstanceType));
            definition.Interfaces.Add(new(module.ImportReference(typeof(ICppInstanceNonGeneric))));

            BuildPointerProperty(definition, out var ptr);
            BuildIsOwnerProperty(definition, out var owns);
            BuildDefaultCtor(definition, ptr, owns);
            BuildImplicitOperator(definition);
            BuildClassSizeProperty(definition, classSize);
            BuildDtor(definition, dtor ?? default, false);
        }

        [MemberNotNull(nameof(ctor_Default), nameof(ctor_Ptr_Owns), nameof(method_ConstructInstance_object), nameof(method_ConstructInstance))]
        void BuildDefaultCtor(TypeDefinition definition, FieldDefinition ptr, FieldDefinition owns)
        {
            var ctorDefault = new MethodDefinition(
                ".ctor",
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName,
                module.TypeSystem.Void);
            {
                var il = ctorDefault.Body.GetILProcessor();
                il.Append(il.Create(oc.Ldarg_0));
                il.Append(il.Create(oc.Call, module.ImportReference(typeof(object).GetConstructors().First())));
                il.Append(il.Create(oc.Ldarg_0));
                il.Append(il.Create(oc.Ldc_I8, 0L));
                il.Append(il.Create(oc.Conv_Ovf_I));
                il.Append(il.Create(oc.Stfld, ptr));
                il.Append(il.Create(oc.Ldarg_0));
                il.Append(il.Create(oc.Ldc_I4_0));
                il.Append(il.Create(oc.Stfld, owns));
                il.Append(il.Create(oc.Ret));
            }

            var ctor = new MethodDefinition(
                ".ctor",
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName,
                module.TypeSystem.Void);
            var ptrParameter = new ParameterDefinition("ptr", ParameterAttributes.None, module.ImportReference(typeof(nint)));
            var ownsParameter = new ParameterDefinition("owns", ParameterAttributes.Optional, module.ImportReference(typeof(bool))) { Constant = false };
            ctor.Parameters.Add(ptrParameter);
            ctor.Parameters.Add(ownsParameter);
            {
                var il = ctor.Body.GetILProcessor();
                il.Append(il.Create(oc.Ldarg_0));
                il.Append(il.Create(oc.Call, module.ImportReference(typeof(object).GetConstructors().First())));
                il.Append(il.Create(oc.Ldarg_0));
                il.Append(il.Create(oc.Ldarg_1));
                il.Append(il.Create(oc.Stfld, ptr));
                il.Append(il.Create(oc.Ldarg_0));
                il.Append(il.Create(oc.Ldarg_2));
                il.Append(il.Create(oc.Stfld, owns));
                il.Append(il.Create(oc.Ret));
            }

            var IcppInstanceType = new GenericInstanceType(module.ImportReference(typeof(ICppInstance<>)));
            IcppInstanceType.GenericArguments.Add(definition);

            var temp = IcppInstanceType.Resolve();

            var methodRef_ConstructInstance = temp.Methods.First(
                f => f.Name == "ConstructInstance" &&
                f.Parameters.Count is 2 &&
                f.Parameters[0].ParameterType.FullName == module.TypeSystem.IntPtr.FullName &&
                f.Parameters[1].ParameterType.FullName == module.TypeSystem.Boolean.FullName &&
                f.ReturnType.FullName == "TSelf");

            var ConstructInstanceMethod = new MethodDefinition(
                "ConstructInstance",
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.Static,
                definition);
            ConstructInstanceMethod.Overrides.Add(module.ImportReference(methodRef_ConstructInstance));
            ConstructInstanceMethod.Parameters.Add(new("ptr", ParameterAttributes.None, module.TypeSystem.IntPtr));
            ConstructInstanceMethod.Parameters.Add(new("owns", ParameterAttributes.None, module.TypeSystem.Boolean));
            {
                var il = ConstructInstanceMethod.Body.GetILProcessor();
                il.Append(il.Create(oc.Ldarg_0));
                il.Append(il.Create(oc.Ldarg_1));
                il.Append(il.Create(oc.Newobj, ctor));
                il.Append(il.Create(oc.Ret));
            }

            var ConstructInstanceNonGenericMethod = new MethodDefinition(
                "ConstructInstance",
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.Static,
                module.TypeSystem.Object);
            ConstructInstanceNonGenericMethod.Overrides.Add(module.ImportReference(
                typeof(ICppInstanceNonGeneric)
                .GetMethods()
                .First(f => f.Name == nameof(ICppInstanceNonGeneric.ConstructInstance))));

            ConstructInstanceNonGenericMethod.Parameters.Add(new("ptr", ParameterAttributes.None, module.TypeSystem.IntPtr));
            ConstructInstanceNonGenericMethod.Parameters.Add(new("owns", ParameterAttributes.None, module.TypeSystem.Boolean));
            {
                var il = ConstructInstanceNonGenericMethod.Body.GetILProcessor();
                il.Append(il.Create(oc.Ldarg_0));
                il.Append(il.Create(oc.Ldarg_1));
                il.Append(il.Create(oc.Call, ConstructInstanceMethod));
                il.Append(il.Create(oc.Ret));
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
        void BuildPointerProperty(TypeDefinition definition, out FieldDefinition ptr)
        {
            var pointerField = new FieldDefinition(
            PointerStorageFieldName, FieldAttributes.Private, module.ImportReference(typeof(nint)));

            var pointerProperty = new PropertyDefinition(
                "Pointer", PropertyAttributes.None, module.ImportReference(typeof(nint)));

            var pointerProperty_getMethod = new MethodDefinition(
                "get_Pointer",
                MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                module.ImportReference(typeof(nint)));
            pointerProperty_getMethod.Overrides.Add(module.ImportReference(
                typeof(ICppInstanceNonGeneric).GetMethods().First(f => f.Name is "get_Pointer")));
            pointerProperty_getMethod.Parameters.Add(new(definition));
            {
                var il = pointerProperty_getMethod.Body.GetILProcessor();
                il.Append(il.Create(oc.Ldarg_0));
                il.Append(il.Create(oc.Ldfld, pointerField));
                il.Append(il.Create(oc.Ret));
            }
            var pointerProperty_setMethod = new MethodDefinition(
                "set_Pointer",
                MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                module.TypeSystem.Void);
            pointerProperty_setMethod.Overrides.Add(module.ImportReference(
                typeof(ICppInstanceNonGeneric).GetMethods().First(f => f.Name is "set_Pointer")));
            pointerProperty_setMethod.Parameters.Add(new(definition));
            pointerProperty_setMethod.Parameters.Add(new("value", ParameterAttributes.None, module.ImportReference(typeof(nint))));
            {
                var il = pointerProperty_setMethod.Body.GetILProcessor();
                il.Append(il.Create(oc.Ldarg_0));
                il.Append(il.Create(oc.Ldarg_1));
                il.Append(il.Create(oc.Stfld, pointerField));
                il.Append(il.Create(oc.Ret));
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
        void BuildIsOwnerProperty(TypeDefinition definition, out FieldDefinition owns)
        {
            var isOwnerField = new FieldDefinition(
            IsOwnerStorageFieldName, FieldAttributes.Private, module.ImportReference(typeof(bool)));

            var isOwnerProperty = new PropertyDefinition(
                "IsOwner", PropertyAttributes.None, module.ImportReference(typeof(bool)));

            var isOwnerProperty_getMethod = new MethodDefinition(
                "get_IsOwner",
                MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                module.ImportReference(typeof(bool)));
            isOwnerProperty_getMethod.Overrides.Add(module.ImportReference(
                typeof(ICppInstanceNonGeneric).GetMethods().First(f => f.Name is "get_IsOwner")));
            isOwnerProperty_getMethod.Parameters.Add(new(definition));
            {
                var il = isOwnerProperty_getMethod.Body.GetILProcessor();
                il.Append(il.Create(oc.Ldarg_0));
                il.Append(il.Create(oc.Ldfld, isOwnerField));
                il.Append(il.Create(oc.Ret));
            }
            var isOwnerProperty_setMethod = new MethodDefinition(
                "set_IsOwner",
                MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                module.TypeSystem.Void);
            isOwnerProperty_setMethod.Overrides.Add(module.ImportReference(
                typeof(ICppInstanceNonGeneric).GetMethods().First(f => f.Name is "set_IsOwner")));
            isOwnerProperty_setMethod.Parameters.Add(new(definition));
            isOwnerProperty_setMethod.Parameters.Add(new("value", ParameterAttributes.None, module.ImportReference(typeof(bool))));
            {
                var il = isOwnerProperty_setMethod.Body.GetILProcessor();
                il.Append(il.Create(oc.Ldarg_0));
                il.Append(il.Create(oc.Ldarg_1));
                il.Append(il.Create(oc.Stfld, isOwnerField));
                il.Append(il.Create(oc.Ret));
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

        void BuildDtor(TypeDefinition definition, in Item item, bool isVirtual, ulong virtualIndex = 0)
        {
            //test
            var method_DestructInstance = new MethodDefinition(
                "DestructInstance",
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.Static,
                module.TypeSystem.Void);
            method_DestructInstance.Overrides.Add(module.ImportReference(
                typeof(ICppInstanceNonGeneric)
                .GetMethods()
                .First(f => f.Name is nameof(ICppInstanceNonGeneric.DestructInstance))));
            method_DestructInstance.Parameters.Add(new("ptr", ParameterAttributes.None, module.TypeSystem.IntPtr));
            {
                var il = method_DestructInstance.Body.GetILProcessor();
                il.Append(il.Create(oc.Nop));
                il.Append(il.Create(oc.Ret));
            }

            var method_Destruct = new MethodDefinition(
                "Destruct",
                MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                module.TypeSystem.Void);
            method_Destruct.Overrides.Add(module.ImportReference(
                typeof(ICppInstanceNonGeneric)
                .GetMethods()
                .First(f => f.Name is nameof(ICppInstanceNonGeneric.Destruct))));
            {
                var il = method_Destruct.Body.GetILProcessor();
                il.Append(il.Create(oc.Ldarg_0));
                il.Append(il.Create(oc.Ldfld, field_Pointer));
                il.Append(il.Create(oc.Call, method_DestructInstance));
                il.Append(il.Create(oc.Ret));
            }

            this.method_Destruct = method_Destruct;
            this.method_DestructInstance = method_DestructInstance;

            definition.Methods.Add(method_Destruct);
            definition.Methods.Add(method_DestructInstance);
        }

        [MemberNotNull(nameof(method_op_Implicit_nint), nameof(method_op_Implicit_voidPtr))]
        void BuildImplicitOperator(TypeDefinition definition)
        {
            var IcppInstanceType = new GenericInstanceType(module.ImportReference(typeof(ICppInstance<>)));
            IcppInstanceType.GenericArguments.Add(definition);

            var op_2_nint = new MethodDefinition(
                "op_Implicit",
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                 MethodAttributes.SpecialName |
                 MethodAttributes.Static,
                module.TypeSystem.IntPtr);
            op_2_nint.Parameters.Add(new("ins", ParameterAttributes.None, definition));

            var opMethod_nint = IcppInstanceType.Resolve().Methods.First(
                f => f.Name == "op_Implicit" &&
                f.Parameters.Count is 1 &&
                f.Parameters[0].ParameterType.FullName == "TSelf" &&
                f.ReturnType.FullName == module.TypeSystem.IntPtr.FullName);

            op_2_nint.Overrides.Add(module.ImportReference(opMethod_nint));
            {
                var il = op_2_nint.Body.GetILProcessor();
                il.Append(il.Create(oc.Ldarg_0));
                var field = definition.Fields.First(f => f.Name is PointerStorageFieldName);
                il.Append(il.Create(oc.Ldfld, field));
                il.Append(il.Create(oc.Ret));
            }


            var op_2_voidPtr = new MethodDefinition(
                "op_Implicit",
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                 MethodAttributes.SpecialName |
                 MethodAttributes.Static,
                module.ImportReference(typeof(void).MakePointerType()));
            op_2_voidPtr.Parameters.Add(new("ins", ParameterAttributes.None, definition));


            var opMethod_voidPtr = IcppInstanceType.Resolve().Methods.First(
                f => f.Name == "op_Implicit" &&
                f.Parameters.Count is 1 &&
                f.Parameters[0].ParameterType.FullName == "TSelf" &&
                f.ReturnType.FullName == module.ImportReference(typeof(void).MakePointerType()).FullName);

            op_2_voidPtr.Overrides.Add(module.ImportReference(opMethod_voidPtr));
            {
                var il = op_2_voidPtr.Body.GetILProcessor();
                il.Append(il.Create(oc.Ldarg_0));
                var field = definition.Fields.First(f => f.Name is PointerStorageFieldName);
                il.Append(il.Create(oc.Ldflda, field));
                il.Append(il.Create(oc.Call, module.ImportReference(typeof(nint).GetMethod(nameof(nint.ToPointer)))));
                il.Append(il.Create(oc.Ret));
            }

            definition.Methods.Add(op_2_nint);
            definition.Methods.Add(op_2_voidPtr);

            method_op_Implicit_nint = op_2_nint;
            method_op_Implicit_voidPtr = op_2_voidPtr;
        }

        [MemberNotNull(nameof(property_ClassSize), nameof(property_ClassSize_getMethod))]
        void BuildClassSizeProperty(TypeDefinition definition, ulong classSize)
        {
            var property = new PropertyDefinition(
                "ClassSize", PropertyAttributes.None, module.TypeSystem.UInt64);

            var getMethod = new MethodDefinition(
                "get_ClassSize",
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.Static,
                module.TypeSystem.UInt64);
            getMethod.Overrides.Add(module.ImportReference(
                typeof(ICppInstanceNonGeneric).GetMethods().First(f => f.Name is "get_ClassSize")));
            {
                var il = getMethod.Body.GetILProcessor();
                il.Append(il.Create(oc.Ldc_I8, (long)classSize));
                il.Append(il.Create(oc.Conv_U8));
                il.Append(il.Create(oc.Ret));
            }

            property.GetMethod = getMethod;

            definition.Properties.Add(property);
            definition.Methods.Add(getMethod);

            property_ClassSize = property;
            property_ClassSize_getMethod = getMethod;
        }
    }
}
