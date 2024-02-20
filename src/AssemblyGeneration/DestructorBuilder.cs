using Hosihikari.NativeInterop.Unmanaged;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Hosihikari.Generation.AssemblyGeneration;

public class DestructorBuilder(ModuleDefinition module)
{
    public enum DtorType
    {
        Normal,
        Virtual,
        Empty
    }

    public unsafe void BuildDtor(
        TypeDefinition definition,
        DtorType type,
        in DtorArgs dtorArgs,
        FieldDefinition field_IsOwner,
        FieldDefinition field_Pointer,
        FieldDefinition field_IsTempStackValue)
    {
        MethodDefinition method_DestructInstance = new(
            "DestructInstance",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.Static,
            module.ImportReference(typeof(void)));
        method_DestructInstance.Overrides.Add(module.ImportReference(
            typeof(ICppInstanceNonGeneric)
                .GetMethods()
                .First(f => f.Name is nameof(ICppInstanceNonGeneric.DestructInstance))));
        method_DestructInstance.Parameters.Add(new("ptr", ParameterAttributes.None,
            module.ImportReference(typeof(nint))));

        CallSite callSite = new(module.ImportReference(typeof(void)))
        {
            CallingConvention = MethodCallingConvention.Unmanaged,
            Parameters = { new(module.ImportReference(typeof(nint))) }
        };
        switch (type)
        {
            case DtorType.Normal:
                {
                    ILProcessor? il = method_DestructInstance.Body.GetILProcessor();
                    il.Emit(OC.Ldarg_0);
                    il.Emit(OC.Call, dtorArgs.propertyDef!.GetMethod);
                    il.Emit(OC.Calli, callSite);
                    il.Emit(OC.Ret);
                }
                break;
            case DtorType.Virtual:
                {
                    VariableDefinition fptr = new(module.ImportReference(typeof(nint)));
                    method_DestructInstance.Body.Variables.Add(fptr);

                    ILProcessor? il = method_DestructInstance.Body.GetILProcessor();
                    il.Emit(OC.Ldarg_0);
                    il.Emit(OC.Ldflda, field_Pointer);
                    il.Emit(OC.Call, module.ImportReference(typeof(nint).GetMethod(nameof(nint.ToPointer))));
                    il.Append(il.Create(OC.Call, module.ImportReference(
                        typeof(CppTypeSystem)
                            .GetMethods()
                            .First(f => f is { Name: "GetVTable", IsGenericMethodDefinition: false }))));
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
                    ILProcessor? il = method_DestructInstance.Body.GetILProcessor();
                    il.Emit(OC.Ret);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, default);
        }

        definition.Methods.Add(method_DestructInstance);

        MethodDefinition method_Destruct = new(
            "Destruct",
            MethodAttributes.Public |
            MethodAttributes.Final |
            MethodAttributes.HideBySig |
            MethodAttributes.NewSlot |
            MethodAttributes.Virtual,
            module.ImportReference(typeof(void)));
        {
            ILProcessor? il = method_Destruct.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldfld, field_Pointer);
            il.Emit(OC.Call, method_DestructInstance);
            il.Emit(OC.Ret);
        }
        definition.Methods.Add(method_Destruct);

        FieldDefinition disposedValueField = new("disposedValue", FieldAttributes.Private,
            module.ImportReference(typeof(bool)));
        definition.Fields.Add(disposedValueField);

        MethodDefinition method_Dispose_virtual = new(
            "Dispose",
            MethodAttributes.Family |
            MethodAttributes.HideBySig |
            MethodAttributes.NewSlot |
            MethodAttributes.Virtual,
            module.ImportReference(typeof(void)));
        method_Dispose_virtual.Parameters.Add(new("disposing", ParameterAttributes.None,
            module.ImportReference(typeof(bool))));
        {
            ILProcessor? il = method_Dispose_virtual.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldfld, disposedValueField);

            Instruction? instruction_test_IsOwner = il.Create(OC.Ldarg_0);
            il.Emit(OC.Brfalse_S, instruction_test_IsOwner);
            il.Emit(OC.Ret);

            il.Append(instruction_test_IsOwner);
            il.Emit(OC.Ldfld, field_IsOwner);

            Instruction? instruction_disposedValue_equals_true = il.Create(OC.Ldarg_0);
            il.Emit(OC.Brfalse_S, instruction_disposedValue_equals_true);

            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Call, method_Destruct);

            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldfld, field_IsTempStackValue);

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

        MethodDefinition method_Dispose = new(
            nameof(IDisposable.Dispose),
            MethodAttributes.Public |
            MethodAttributes.Final |
            MethodAttributes.HideBySig |
            MethodAttributes.NewSlot |
            MethodAttributes.Virtual,
            module.ImportReference(typeof(void)));
        {
            ILProcessor? il = method_Dispose.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldc_I4_1);
            il.Emit(OC.Callvirt, method_Dispose_virtual);
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Call,
                module.ImportReference(Utils.GC.GetMethods().First(t => t.Name is nameof(GC.SuppressFinalize))));
            il.Emit(OC.Ret);
        }
        definition.Methods.Add(method_Dispose);

        MethodDefinition method_Finalize = new(
            "Finalize",
            MethodAttributes.Family |
            MethodAttributes.HideBySig |
            MethodAttributes.Virtual,
            module.ImportReference(typeof(void)));
        method_Finalize.Overrides.Add(
            module.ImportReference(Utils.Object.GetMethods().First(m => m.Name is "Finalize")));
        {
            ILProcessor? il = method_Finalize.Body.GetILProcessor();

            Instruction? try_start = il.Create(OC.Ldarg_0);
            Instruction? finally_start = il.Create(OC.Ldarg_0);
            Instruction? finally_end = il.Create(OC.Ret);
            Instruction? try_leave = il.Create(OC.Leave_S, finally_end);

            il.Append(try_start);
            il.Emit(OC.Ldc_I4_0);
            il.Emit(OC.Callvirt, method_Dispose_virtual);
            il.Append(try_leave);

            il.Append(finally_start);
            il.Emit(OC.Call, module.ImportReference(Utils.Object.GetMethods().First(m => m.Name is "Finalize")));
            il.Append(finally_end);

            ExceptionHandler handler = new(ExceptionHandlerType.Finally)
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

    public struct DtorArgs()
    {
        public int? virtualIndex = default;
        public PropertyDefinition? propertyDef = default;
    }
}