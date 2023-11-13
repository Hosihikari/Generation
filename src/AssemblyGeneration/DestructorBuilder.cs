using Hosihikari.NativeInterop.Unmanaged;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

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

    public unsafe void BuildDtor(
        TypeDefinition definition,
        DtorType type,
        in DtorArgs dtorArgs,
        FieldDefinition field_IsOwner,
        FieldDefinition field_Pointer,
        FieldDefinition field_IsTempStackValue)
    {
        var method_DestructInstance = new MethodDefinition(
            "DestructInstance",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.Static,
            module.ImportReference(typeof(void)));
        method_DestructInstance.Overrides.Add(module.ImportReference(
            typeof(ICppInstanceNonGeneric)
            .GetMethods()
            .First(f => f.Name is nameof(ICppInstanceNonGeneric.DestructInstance))));
        method_DestructInstance.Parameters.Add(new("ptr", ParameterAttributes.None, module.ImportReference(typeof(nint))));

        var callSite = new CallSite(module.ImportReference(typeof(void)))
        {
            CallingConvention = MethodCallingConvention.Unmanaged,
            Parameters = { new(module.ImportReference(typeof(nint))) }
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
                    var fptr = new VariableDefinition(module.ImportReference(typeof(nint)));
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
        definition.Methods.Add(method_DestructInstance);

        var method_Destruct = new MethodDefinition(
            "Destruct",
            MethodAttributes.Public |
            MethodAttributes.Final |
            MethodAttributes.HideBySig |
            MethodAttributes.NewSlot |
            MethodAttributes.Virtual,
            module.ImportReference(typeof(void)));
        {
            var il = method_Destruct.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldfld, field_Pointer);
            il.Emit(OC.Call, method_DestructInstance);
            il.Emit(OC.Ret);
        }
        definition.Methods.Add(method_Destruct);

        var disposedValueField = new FieldDefinition("disposedValue", FieldAttributes.Private, module.ImportReference(typeof(bool)));
        definition.Fields.Add(disposedValueField);

        var method_Dispose_virtual = new MethodDefinition(
            "Dispose",
            MethodAttributes.Family |
            MethodAttributes.HideBySig |
            MethodAttributes.NewSlot |
            MethodAttributes.Virtual,
            module.ImportReference(typeof(void)));
        method_Dispose_virtual.Parameters.Add(new("disposing", ParameterAttributes.None, module.ImportReference(typeof(bool))));
        {
            var il = method_Dispose_virtual.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldfld, disposedValueField);

            var instruction_test_IsOwner = il.Create(OC.Ldarg_0);
            il.Emit(OC.Brfalse_S, instruction_test_IsOwner);
            il.Emit(OC.Ret);

            il.Append(instruction_test_IsOwner);
            il.Emit(OC.Ldfld, field_IsOwner);

            var instruction_test_IsTempStackValue_is_false = il.Create(OC.Ldarg_0);
            il.Emit(OC.Brfalse_S, instruction_test_IsTempStackValue_is_false);

            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Call, method_Destruct);

            il.Append(instruction_test_IsTempStackValue_is_false);
            il.Emit(OC.Ldfld, field_IsTempStackValue);

            var instruction_disposedValue_equals_true = il.Create(OC.Ldarg_0);
            il.Emit(OC.Brtrue_S, instruction_disposedValue_equals_true);

            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldflda, field_Pointer);
            il.Emit(OC.Call, module.ImportReference(typeof(nint).GetMethod(nameof(nint.ToPointer))));
            il.Emit(OC.Call, module.ImportReference(typeof(HeapAlloc).GetMethod(nameof(HeapAlloc.Delete))));

            il.Append(instruction_disposedValue_equals_true);
            il.Emit(OC.Ldc_I4_1);
            il.Emit(OC.Stfld, disposedValueField);

            il.Emit(OC.Ret);
        }
        definition.Methods.Add(method_Dispose_virtual);

        definition.Interfaces.Add(new(module.ImportReference(Utils.IDisposable)));

        var method_Dispose = new MethodDefinition(
                nameof(IDisposable.Dispose),
                MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                module.ImportReference(typeof(void)));
        {
            var il = method_Dispose.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldc_I4_1);
            il.Emit(OC.Callvirt, method_Dispose_virtual);
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Call, module.ImportReference(Utils.GC.GetMethods().First(t => t.Name is nameof(GC.SuppressFinalize))));
            il.Emit(OC.Ret);
        }
        definition.Methods.Add(method_Dispose);

        var method_Finalize = new MethodDefinition(
            "Finalize",
            MethodAttributes.Family |
            MethodAttributes.HideBySig |
            MethodAttributes.Virtual,
            module.ImportReference(typeof(void)));
        method_Finalize.Overrides.Add(module.ImportReference(Utils.Object.GetMethods().First(m => m.Name is "Finalize")));
        {
            var il = method_Finalize.Body.GetILProcessor();

            var try_start = il.Create(OC.Ldarg_0);
            var finally_start = il.Create(OC.Ldarg_0);
            var finally_end = il.Create(OC.Ret);
            var try_leave = il.Create(OC.Leave_S, finally_end);

            il.Append(try_start);
            il.Emit(OC.Ldc_I4_0);
            il.Emit(OC.Callvirt, method_Dispose_virtual);
            il.Append(try_leave);

            il.Append(finally_start);
            il.Emit(OC.Call, module.ImportReference(Utils.Object.GetMethods().First(m => m.Name is "Finalize")));
            il.Append(finally_end);

            var handler = new ExceptionHandler(ExceptionHandlerType.Finally)
            {
                TryStart = try_start,
                TryEnd = try_leave.Next,
                HandlerStart = finally_start,
                HandlerEnd = finally_end
            };

            method_Finalize.Body.ExceptionHandlers.Add(handler);
        }
        definition.Methods.Add(method_Finalize);
    }

}
