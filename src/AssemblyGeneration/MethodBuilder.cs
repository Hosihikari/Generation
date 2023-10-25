using Hosihikari.Utils;
using Hosihikari.NativeInterop.Unmanaged;
using Mono.Cecil;
using Mono.Cecil.Cil;
using static Hosihikari.Generation.AssemblyGeneration.AssemblyBuilder;
using static Hosihikari.Utils.OriginalData.Class;
using static Hosihikari.Utils.OriginalData;
using static Hosihikari.Generation.AssemblyGeneration.DestructorBuilder;

namespace Hosihikari.Generation.AssemblyGeneration;

public class MethodBuilder
{

    public readonly ModuleDefinition module;

    public MethodBuilder(ModuleDefinition module)
    {
        this.module = module;
    }

    public MethodDefinition BuildCtor(
        ItemAccessType itemAccessType,
        PropertyDefinition fptrProperty,
        FunctionPointerType functionPointer,
        bool isVarArg,
        FieldDefinition field_Pointer,
        FieldDefinition field_IsOwner,
        FieldDefinition field_IsTempStackValue,
        ulong classSize,
        in Item t)
    {
        (int begin, int end) loopRange = (1/*[nint]@this*/, functionPointer.Parameters.Count);
        if (isVarArg) loopRange.end -= 1;

        var ctor = new MethodDefinition(
            ".ctor",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName,
            module.TypeSystem.Void);
        if (isVarArg) ctor.CallingConvention |= MethodCallingConvention.VarArg;

        if (classSize is 0)
            ctor.Parameters.Add(new("allocSize", ParameterAttributes.None, module.TypeSystem.UInt64));
        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            ParameterDefinition param = functionPointer.Parameters[i];
            ctor.Parameters.Add(new($"a{i - 1}", ParameterAttributes.None, param.ParameterType));
        }

        {
            var fptr = new VariableDefinition(module.ImportReference(typeof(void).MakePointerType()));
            ctor.Body.Variables.Add(fptr);
            var il = ctor.Body.GetILProcessor();
            il.Append(il.Create(OC.Ldarg_0));
            il.Append(il.Create(OC.Ldarg_1));
            il.Append(il.Create(OC.Call, module.ImportReference(typeof(HeapAlloc).GetMethod(nameof(HeapAlloc.New)))));
            il.Append(il.Create(OC.Stfld, field_Pointer));

            il.Append(il.Create(OC.Call, fptrProperty.GetMethod));
            il.Append(il.Create(OC.Stloc, fptr));

            il.Append(il.Create(OC.Ldarg_0));
            il.Append(il.Create(OC.Ldfld, field_Pointer));
            for (int i = loopRange.begin; i < loopRange.end; i++)
                il.Append(il.Create(OC.Ldarg, i + 1));

            var callSite = new CallSite(module.ImportReference(module.TypeSystem.Void))
            {
                CallingConvention = MethodCallingConvention.Unmanaged
            };
            foreach (var param in functionPointer.Parameters)
                callSite.Parameters.Add(param);

            if (isVarArg) il.Append(il.Create(OC.Arglist));

            il.Append(il.Create(OC.Ldloc, fptr));
            il.Append(il.Create(OC.Calli, callSite));

            il.Append(il.Create(OC.Ldarg_0));
            il.Append(il.Create(OC.Ldc_I4_1));
            il.Append(il.Create(OC.Stfld, field_IsOwner));

            il.Append(il.Create(OC.Ldarg_0));
            il.Append(il.Create(OC.Ldc_I4_0));
            il.Append(il.Create(OC.Stfld, field_IsTempStackValue));
            il.Append(il.Create(OC.Ret));
        }

        return ctor;
    }

    public MethodDefinition BuildFunction(
        ItemAccessType itemAccessType,
        PropertyDefinition fptrProperty,
        FunctionPointerType functionPointer,
        bool isVarArg,
        FieldDefinition field_Pointer,
        in Item t)
    {
        var hasThis = Utils.HasThis(itemAccessType);

        var attributes = MethodAttributes.Public;
        if (Utils.HasThis(itemAccessType) is false) attributes |= MethodAttributes.Static;

        var methodName = t.Name.Contains("operator") ?
            Utils.SelectOperatorName(t) :
            t.Name.Length > 1 ? $"{char.ToUpper(t.Name[0])}{t.Name[1..]}" : t.Name.ToUpper();

        (int begin, int end) loopRange = (0, functionPointer.Parameters.Count);
        if (hasThis) loopRange.begin += 1;
        if (isVarArg) loopRange.end -= 1;

        var method = new MethodDefinition(methodName, attributes, functionPointer.ReturnType);
        if (isVarArg) method.CallingConvention |= MethodCallingConvention.VarArg;

        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            ParameterDefinition param = functionPointer.Parameters[i];
            method.Parameters.Add(new($"a{i - (hasThis ? 1 : 0)}", ParameterAttributes.None, param.ParameterType));
        }

        {
            var fptr = new VariableDefinition(module.ImportReference(typeof(void).MakePointerType()));
            method.Body.Variables.Add(fptr);
            var il = method.Body.GetILProcessor();

            var callSite = new CallSite(module.ImportReference(functionPointer.ReturnType))
            {
                CallingConvention = MethodCallingConvention.Unmanaged
            };
            foreach (var param in functionPointer.Parameters)
                callSite.Parameters.Add(param);

            il.Append(il.Create(OC.Call, fptrProperty.GetMethod));
            il.Append(il.Create(OC.Stloc, fptr));

            if (hasThis)
            {
                il.Append(il.Create(OC.Ldarg_0));
                il.Append(il.Create(OC.Ldfld, field_Pointer));
            }
            for (int i = loopRange.begin; i < loopRange.end; i++)
                il.Append(il.Create(OC.Ldarg, i));

            if (isVarArg) il.Append(il.Create(OC.Arglist));

            il.Append(il.Create(OC.Ldloc, fptr));
            il.Append(il.Create(OC.Calli, callSite));
            il.Append(il.Create(OC.Ret));
        }

        return method;
    }

    public MethodDefinition? BuildMethod(
        ItemAccessType itemAccessType,
        PropertyDefinition fptrProperty,
        FunctionPointerType functionPointer,
        bool isVarArg,
        FieldDefinition field_Pointer,
        FieldDefinition field_IsOwner,
        FieldDefinition field_IsTempStackValue,
        ulong classSize,
        in Item t,
        Action ifIsDtor)
    {
        switch ((SymbolType)t.SymbolType)
        {
            case SymbolType.Function:
            case SymbolType.Operator:
                return BuildFunction(itemAccessType, fptrProperty, functionPointer, isVarArg, field_Pointer, t);

            case SymbolType.Constructor:
                return BuildCtor(itemAccessType, fptrProperty, functionPointer, isVarArg, field_Pointer, field_IsOwner,
                    field_IsTempStackValue, classSize, t);

            case SymbolType.Destructor:
                ifIsDtor();
                return null;

            case SymbolType.StaticField:
            case SymbolType.UnknownFunction:
                return null;
        }

        return null;
    }

    public unsafe MethodDefinition? BuildVirtualMethod(
        FunctionPointerType functionPointer,
        bool isVarArg,
        FieldDefinition field_Pointer,
        int virtIndex,
        in Item t,
        Action ifIsDtor)
    {
        if ((SymbolType)t.SymbolType is SymbolType.Destructor)
        {
            ifIsDtor();
            return null;
        }

        if (virtIndex < 0) throw new InvalidOperationException();

        var methodName = t.Name.Contains("operator") ?
            Utils.SelectOperatorName(t) :
            t.Name.Length > 1 ? $"{char.ToUpper(t.Name[0])}{t.Name[1..]}" : t.Name.ToUpper();

        (int begin, int end) loopRange = (1/*[nint]@this*/, functionPointer.Parameters.Count);
        if (isVarArg) loopRange.end -= 1;

        var method = new MethodDefinition(methodName, MethodAttributes.Public, functionPointer.ReturnType);
        if (isVarArg) method.CallingConvention |= MethodCallingConvention.VarArg;

        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            ParameterDefinition param = functionPointer.Parameters[i];
            method.Parameters.Add(new($"a{i - 1}", ParameterAttributes.None, param.ParameterType));
        }

        {
            var fptr = new VariableDefinition(module.ImportReference(typeof(void).MakePointerType()));
            method.Body.Variables.Add(fptr);
            var il = method.Body.GetILProcessor();

            var callSite = new CallSite(module.ImportReference(functionPointer.ReturnType))
            {
                CallingConvention = MethodCallingConvention.Unmanaged
            };
            foreach (var param in functionPointer.Parameters)
                callSite.Parameters.Add(param);

            il.Append(il.Create(OC.Ldarg_0));
            il.Append(il.Create(OC.Call, module.ImportReference(typeof(CppTypeSystem)
                        .GetMethods()
                        .First(f => f.Name is "GetVTable" && f.IsGenericMethodDefinition is false))));
            il.Append(il.Create(OC.Ldc_I4, sizeof(void*) * virtIndex));
            il.Append(il.Create(OC.Add));
            il.Append(il.Create(OC.Ldind_I));
            il.Append(il.Create(OC.Stloc, fptr));

            il.Append(il.Create(OC.Ldarg_0));
            il.Append(il.Create(OC.Ldfld, field_Pointer));

            for (int i = loopRange.begin; i < loopRange.end; i++)
                il.Append(il.Create(OC.Ldarg, i));

            if (isVarArg) il.Append(il.Create(OC.Arglist));

            il.Append(il.Create(OC.Ldloc, fptr));
            il.Append(il.Create(OC.Calli, callSite));
            il.Append(il.Create(OC.Ret));
        }


        return method;
    }
}
