using Hosihikari.NativeInterop.Unmanaged;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Hosihikari.Generation.AssemblyGeneration;

public class DestructorBuilder
{
    public ModuleDefinition module;

    public DestructorBuilder(ModuleDefinition module)
    {
        this.module = module;
    }

    public enum DtorType { Normal, Virtual, Empty }

    public struct DtorArgs
    {
        public int? virtualIndex = null;
        public PropertyDefinition? propertyDef = null;

        public DtorArgs() { }
    }

    public unsafe (MethodDefinition method_Destruct, MethodDefinition method_DestructInstance, DtorType dtorType)
        BuildDtor(
        DtorType type,
        in DtorArgs dtorArgs,
        FieldDefinition field_Pointer)
    {
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

        var callSite = new CallSite(module.TypeSystem.Void)
        {
            CallingConvention = MethodCallingConvention.Unmanaged,
            Parameters = { new(module.TypeSystem.IntPtr) }
        };
        switch (type)
        {
            case DtorType.Normal:
                {
                    var il = method_DestructInstance.Body.GetILProcessor();
                    il.Emit(OC.Ldarg_0);
                    il.Emit(OC.Call, dtorArgs.propertyDef!.GetMethod);
                    il.Emit(OC.Calli, callSite);
                    il.Emit(OC.Ret);
                }
                break;
            case DtorType.Virtual:
                {
                    var fptr = new VariableDefinition(module.TypeSystem.IntPtr);
                    method_DestructInstance.Body.Variables.Add(fptr);

                    var il = method_DestructInstance.Body.GetILProcessor();
                    il.Emit(OC.Ldarg_0);
                    il.Emit(OC.Ldflda, field_Pointer);
                    il.Emit(OC.Call, module.ImportReference(typeof(nint).GetMethod(nameof(nint.ToPointer))));
                    il.Append(il.Create(OC.Call, module.ImportReference(
                        typeof(CppTypeSystem)
                        .GetMethods()
                        .First(f => f.Name is "GetVTable" && f.IsGenericMethodDefinition is false))));
                    il.Emit(OC.Ldc_I4, sizeof(void*) * dtorArgs.virtualIndex!.Value);
                    il.Emit(OC.Add);
                    il.Emit(OC.Ldind_I);
                    il.Emit(OC.Stloc, fptr);

                    il.Emit(OC.Ldarg_0);
                    il.Emit(OC.Ldloc, fptr);
                    il.Emit(OC.Calli, callSite);
                    il.Emit(OC.Ret);
                }
                break;
            case DtorType.Empty:
                {
                    var il = method_DestructInstance.Body.GetILProcessor();
                    il.Emit(OC.Ret);
                }
                break;
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
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldfld, field_Pointer);
            il.Emit(OC.Call, method_DestructInstance);
            il.Emit(OC.Ret);
        }

        return (method_Destruct, method_DestructInstance, type);
    }

}
