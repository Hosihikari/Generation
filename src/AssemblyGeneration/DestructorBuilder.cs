using Hosihikari.NativeInterop.Unmanaged;
using Mono.Cecil;
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
                    il.Append(il.Create(OC.Ldarg_0));
                    il.Append(il.Create(OC.Ldflda, field_Pointer));
                    il.Append(il.Create(OC.Call, dtorArgs.propertyDef!.GetMethod));
                    il.Append(il.Create(OC.Calli, callSite));
                    il.Append(il.Create(OC.Ret));
                }
                break;
            case DtorType.Virtual:
                {
                    var il = method_DestructInstance.Body.GetILProcessor();
                    il.Append(il.Create(OC.Ldarg_0));
                    il.Append(il.Create(OC.Ldflda, field_Pointer));
                    il.Append(il.Create(OC.Call, module.ImportReference(typeof(nint).GetMethod(nameof(nint.ToPointer)))));
                    il.Append(il.Create(OC.Call, module.ImportReference(
                        typeof(CppTypeSystem)
                        .GetMethods()
                        .First(f => f.Name is "GetVTable" && f.IsGenericMethodDefinition is false))));
                    il.Append(il.Create(OC.Add, sizeof(void*) * dtorArgs.virtualIndex!.Value));
                    il.Append(il.Create(OC.Ldind_I));
                    il.Append(il.Create(OC.Calli, callSite));
                    il.Append(il.Create(OC.Ret));
                }
                break;
            case DtorType.Empty:
                {
                    var il = method_DestructInstance.Body.GetILProcessor();
                    il.Append(il.Create(OC.Ret));
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
            il.Append(il.Create(OC.Ldarg_0));
            il.Append(il.Create(OC.Ldfld, field_Pointer));
            il.Append(il.Create(OC.Call, method_DestructInstance));
            il.Append(il.Create(OC.Ret));
        }

        return (method_Destruct, method_DestructInstance, type);
    }

}
