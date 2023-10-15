using Hosihikari.NativeInterop.Unmanaged;
using Mono.Cecil;
using static Hosihikari.Utils.OriginalData.Class;

namespace Hosihikari.Generation.AssemblyGeneration
{

    public partial class AssemblyBuilder
    {
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
            BuildCtor(definition, ptr, owns);
        }

        void BuildCtor(TypeDefinition definition, FieldDefinition ptr, FieldDefinition owns)
        {
            var ctor = new MethodDefinition(
                ".ctor",
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName,
                module.TypeSystem.Void);
            var ptrParameter = new ParameterDefinition("ptr", ParameterAttributes.None, module.ImportReference(typeof(nint)));
            var ownsParameter = new ParameterDefinition("owns", ParameterAttributes.Optional, module.ImportReference(typeof(bool))) { Constant = false };
            ctor.Parameters.Add(new(definition));
            ctor.Parameters.Add(ptrParameter);
            ctor.Parameters.Add(ownsParameter);
            {
                var il = ctor.Body.GetILProcessor();
                il.Append(il.Create(oc.Ldarg_0));
                il.Append(il.Create(oc.Ldarg_1));
                il.Append(il.Create(oc.Stfld, ptr));
                il.Append(il.Create(oc.Ldarg_0));
                il.Append(il.Create(oc.Ldarg_2));
                il.Append(il.Create(oc.Stfld, owns));
                il.Append(il.Create(oc.Ret));
            }

            definition.Methods.Add(ctor);
        }

        void BuildPointerProperty(TypeDefinition definition, out FieldDefinition ptr)
        {
            var pointerField = new FieldDefinition(
            "__pointer_storage", FieldAttributes.Private, module.ImportReference(typeof(nint)));

            var pointerProperty = new PropertyDefinition(
                "Pointer", PropertyAttributes.None, module.ImportReference(typeof(nint)));

            var pointerProperty_getMethod = new MethodDefinition(
                "get_Pointer",
                MethodAttributes.Public |
                MethodAttributes.Virtual |
                MethodAttributes.SpecialName |
                MethodAttributes.HideBySig,
                module.ImportReference(typeof(nint)));
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
                MethodAttributes.Virtual |
                MethodAttributes.SpecialName |
                MethodAttributes.HideBySig,
                module.TypeSystem.Void);
            pointerProperty_setMethod.Parameters.Add(new(definition));
            pointerProperty_setMethod.Parameters.Add(new(module.ImportReference(typeof(nint))));
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
        }

        void BuildIsOwnerProperty(TypeDefinition definition, out FieldDefinition owns)
        {
            var isOwnerField = new FieldDefinition(
            "__isOwner_storage", FieldAttributes.Private, module.ImportReference(typeof(bool)));

            var isOwnerProperty = new PropertyDefinition(
                "IsOwner", PropertyAttributes.None, module.ImportReference(typeof(bool)));

            var isOwnerProperty_getMethod = new MethodDefinition(
                "get_IsOwner",
                MethodAttributes.Public |
                MethodAttributes.Virtual |
                MethodAttributes.SpecialName |
                MethodAttributes.HideBySig,
                module.ImportReference(typeof(bool)));
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
                MethodAttributes.Virtual |
                MethodAttributes.SpecialName |
                MethodAttributes.HideBySig,
                module.TypeSystem.Void);
            isOwnerProperty_setMethod.Parameters.Add(new(definition));
            isOwnerProperty_setMethod.Parameters.Add(new(module.ImportReference(typeof(bool))));
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
        }
    }
}
