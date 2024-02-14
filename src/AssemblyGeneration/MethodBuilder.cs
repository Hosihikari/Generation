using Hosihikari.Generation.Utils;
using Hosihikari.NativeInterop.Unmanaged;
using Hosihikari.NativeInterop.Unmanaged.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Runtime.CompilerServices;
using static Hosihikari.Generation.AssemblyGeneration.AssemblyBuilder;
using static Hosihikari.Generation.Utils.OriginalData.Class;
using CallSite = Mono.Cecil.CallSite;
using ExtensionAttribute = System.Runtime.CompilerServices.ExtensionAttribute;

namespace Hosihikari.Generation.AssemblyGeneration;

public class MethodBuilder(ModuleDefinition module)
{
    /// <summary>
    /// Builds a constructor method definition.
    /// </summary>
    /// <param name="fptrProperty">The property definition for the function pointer.</param>
    /// <param name="functionPointer">The method signature for the function pointer.</param>
    /// <param name="isVarArg">Indicates if the function has a variable number of arguments.</param>
    /// <param name="field_Pointer">The field reference for the pointer.</param>
    /// <param name="field_IsOwner">The field reference for the owner flag.</param>
    /// <param name="field_IsTempStackValue">The field reference for the temporary stack value flag.</param>
    /// <param name="classSize">The size of the class.</param>
    /// <param name="t">The item.</param>
    /// <returns>The constructed method definition.</returns>
    private MethodDefinition BuildCtor(PropertyDefinition fptrProperty, IMethodSignature functionPointer, bool isVarArg, FieldReference field_Pointer, FieldReference field_IsOwner, FieldReference field_IsTempStackValue, ulong classSize, in Item t)
    {
        // Determine the loop range based on the function pointer parameters and whether it's a vararg function
        (int begin, int end) loopRange = (1, functionPointer.Parameters.Count);
        if (isVarArg) loopRange.end -= 1;

        // Create a new method definition for the constructor
        MethodDefinition ctor = new(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.ImportReference(typeof(void)));
        if (isVarArg) ctor.CallingConvention |= MethodCallingConvention.VarArg;
        if (classSize is 0) ctor.Parameters.Add(new("allocSize", ParameterAttributes.None, module.ImportReference(typeof(ulong))));

        // Add parameters to the constructor based on the function pointer parameters
        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            ParameterDefinition param = functionPointer.Parameters[i];
            ctor.Parameters.Add(new(Utils.GetParameterName(t, true, i), ParameterAttributes.None, param.ParameterType));
        }

        // Create a variable for the function pointer
        VariableDefinition fptr = new(module.ImportReference(typeof(void).MakePointerType()));
        ctor.Body.Variables.Add(fptr);
        ILProcessor? il = ctor.Body.GetILProcessor();

        // Emit IL instructions for constructor body
        il.Emit(OC.Ldarg_0); // Load argument 0
        il.Emit(OC.Call, module.ImportReference(Utils.Object.GetConstructors().First())); // Call the first constructor of the object
        il.Emit(OC.Ldarg_0); // Load argument 0

        if (classSize is 0)
        {
            il.Emit(OC.Ldarg_1); // Load argument 1
        }
        else
        {
            il.Emit(OC.Ldc_I8, classSize); // Load constant of type long
            il.Emit(OC.Conv_U8); // Convert to unsigned long
        }

        il.Emit(OC.Call, module.ImportReference(typeof(HeapAlloc).GetMethod(nameof(HeapAlloc.New)))); // Call the New method of HeapAlloc
        il.Emit(OC.Stfld, field_Pointer); // Store the field Pointer
        il.Emit(OC.Call, fptrProperty.GetMethod); // Call the get method of the fptrProperty
        il.Emit(OC.Stloc, fptr); // Store the value in fptr
        il.Emit(OC.Ldarg_0); // Load argument 0
        il.Emit(OC.Ldfld, field_Pointer); // Load the field Pointer

        for (int i = classSize is 0 ? 1 : 0; i < (ctor.Parameters.Count - (isVarArg ? 1 : 0)); i++)
        {
            il.Emit(OC.Ldarg_S, ctor.Parameters[i]); // Load the argument at index i
        }

        CallSite callSite = new(module.ImportReference(module.ImportReference(typeof(void)))) { CallingConvention = MethodCallingConvention.Unmanaged }; // Create a new CallSite
        foreach (ParameterDefinition? param in functionPointer.Parameters)
        {
            callSite.Parameters.Add(param); // Add parameter to the CallSite
        }
        if (isVarArg)
        {
            il.Emit(OC.Arglist); // Emit the Arglist opcode
        }
        il.Emit(OC.Ldloc, fptr); // Load the value in fptr
        il.Emit(OC.Calli, callSite); // Call the function pointer using the CallSite
        il.Emit(OC.Ldarg_0); // Load argument 0
        il.Emit(OC.Ldc_I4_1); // Load the constant 1
        il.Emit(OC.Stfld, field_IsOwner); // Store the value in field IsOwner
        il.Emit(OC.Ldarg_0); // Load argument 0
        il.Emit(OC.Ldc_I4_0); // Load the constant 0
        il.Emit(OC.Stfld, field_IsTempStackValue); // Store the value in field IsTempStackValue
        il.Emit(OC.Ret); // Return

        return ctor;
    }



    private MethodDefinition BuildFunction(
        ItemAccessType itemAccessType,
        PropertyDefinition fptrProperty,
        IMethodSignature functionPointer,
        bool isVarArg,
        FieldReference field_Pointer,
        in Item t)
    {
        bool hasThis = Utils.HasThis(itemAccessType);

        MethodAttributes attributes = MethodAttributes.Public;
        if (Utils.HasThis(itemAccessType) is false)
        {
            attributes |= MethodAttributes.Static;
        }

        string methodName = t.Name.Contains("operator") ? Utils.SelectOperatorName(t) :
            t.Name.Length > 1 ? $"{char.ToUpper(t.Name[0])}{t.Name[1..]}" : t.Name.ToUpper();

        (int begin, int end) loopRange = (0, functionPointer.Parameters.Count);
        if (hasThis)
        {
            loopRange.begin += 1;
        }

        if (isVarArg)
        {
            loopRange.end -= 1;
        }

        MethodDefinition method = new(methodName, attributes, functionPointer.ReturnType);
        if (isVarArg)
        {
            method.CallingConvention |= MethodCallingConvention.VarArg;
        }

        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            ParameterDefinition param = functionPointer.Parameters[i];
            method.Parameters.Add(new(Utils.GetParameterName(t, hasThis, i), ParameterAttributes.None,
                param.ParameterType));
        }

        {
            VariableDefinition fptr = new(module.ImportReference(typeof(void).MakePointerType()));
            method.Body.Variables.Add(fptr);
            ILProcessor? il = method.Body.GetILProcessor();

            CallSite callSite = new(module.ImportReference(functionPointer.ReturnType))
            {
                CallingConvention = MethodCallingConvention.Unmanaged
            };
            foreach (ParameterDefinition? param in functionPointer.Parameters)
            {
                callSite.Parameters.Add(param);
            }

            il.Emit(OC.Call, fptrProperty.GetMethod);
            il.Emit(OC.Stloc, fptr);

            if (hasThis)
            {
                il.Emit(OC.Ldarg_0);
                il.Emit(OC.Ldfld, field_Pointer);
            }

            for (int i = loopRange.begin; i < loopRange.end; i++)
            {
                il.Emit(OC.Ldarg, i);
            }

            if (isVarArg)
            {
                il.Emit(OC.Arglist);
            }

            il.Emit(OC.Ldloc, fptr);
            il.Emit(OC.Calli, callSite);
            il.Emit(OC.Ret);
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
        MethodDefinition? ret = null;

        switch ((SymbolType)t.SymbolType)
        {
            case SymbolType.Function:
            case SymbolType.Operator:
                ret = BuildFunction(itemAccessType, fptrProperty, functionPointer, isVarArg, field_Pointer, t);
                break;

            case SymbolType.Constructor:
                ret = BuildCtor(fptrProperty, functionPointer, isVarArg, field_Pointer, field_IsOwner,
                    field_IsTempStackValue, classSize, t);
                break;

            case SymbolType.Destructor:
                ifIsDtor();
                break;

            case SymbolType.StaticField:
            case SymbolType.UnknownFunction:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (ret is null)
        {
            return ret;
        }

        CustomAttribute attr = new(module.ImportReference(typeof(SymbolAttribute).GetConstructors().First()));
        attr.ConstructorArguments.Add(new(Utils.String, t.Symbol));
        ret.CustomAttributes.Add(attr);

        attr = new(module.ImportReference(typeof(RVAAttribute).GetConstructors().First()));
        attr.ConstructorArguments.Add(new(module.ImportReference(typeof(ulong)), t.RVA));
        ret.CustomAttributes.Add(attr);

        return ret;
    }

    public MethodDefinition? BuildVirtualMethod(
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

        if (virtIndex < 0)
        {
            throw new InvalidOperationException();
        }

        string methodName = t.Name.Contains("operator") ? Utils.SelectOperatorName(t) :
            t.Name.Length > 1 ? $"{char.ToUpper(t.Name[0])}{t.Name[1..]}" : t.Name.ToUpper();

        (int begin, int end) loopRange = (1, functionPointer.Parameters.Count);
        if (isVarArg)
        {
            loopRange.end -= 1;
        }

        MethodDefinition method = new(methodName, MethodAttributes.Public, functionPointer.ReturnType);
        if (isVarArg)
        {
            method.CallingConvention |= MethodCallingConvention.VarArg;
        }

        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            ParameterDefinition param = functionPointer.Parameters[i];
            method.Parameters.Add(
                new(Utils.GetParameterName(t, true, i), ParameterAttributes.None, param.ParameterType));
        }

        {
            VariableDefinition fptr = new(module.ImportReference(typeof(void).MakePointerType()));
            method.Body.Variables.Add(fptr);
            ILProcessor? il = method.Body.GetILProcessor();

            CallSite callSite = new(module.ImportReference(functionPointer.ReturnType))
            {
                CallingConvention = MethodCallingConvention.Unmanaged
            };
            foreach (ParameterDefinition? param in functionPointer.Parameters)
            {
                callSite.Parameters.Add(param);
            }

            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldfld, field_Pointer);
            il.Emit(OC.Ldc_I4, virtIndex);
            il.Append(il.Create(OC.Call, module.ImportReference(typeof(CppTypeSystem)
                .GetMethods()
                .First(f => f is
                {
                    Name: nameof(CppTypeSystem.GetVurtualFunctionPointerByIndex), IsGenericMethodDefinition: false
                }))));
            il.Emit(OC.Stloc, fptr);

            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldfld, field_Pointer);

            for (int i = loopRange.begin; i < loopRange.end; i++)
            {
                il.Emit(OC.Ldarg, i);
            }

            if (isVarArg)
            {
                il.Emit(OC.Arglist);
            }

            il.Emit(OC.Ldloc, fptr);
            il.Emit(OC.Calli, callSite);
            il.Emit(OC.Ret);
        }


        return method;
    }

    public MethodDefinition? BuildExtensionMethod(
        ItemAccessType itemAccessType,
        PropertyDefinition fptrProperty,
        FunctionPointerType functionPointer,
        bool isVarArg,
        in Item t,
        Type extensionType,
        Action ifIsDtor)
    {
        MethodDefinition? ret = null;

        switch ((SymbolType)t.SymbolType)
        {
            case SymbolType.Function:
            case SymbolType.Operator:
                ret = BuildExtensionFunction(itemAccessType, fptrProperty, functionPointer, isVarArg, extensionType, t);
                break;

            case SymbolType.Constructor:
                ret = BuildExtensionCtor(fptrProperty, functionPointer, isVarArg, extensionType, t);
                break;

            case SymbolType.Destructor:
                ifIsDtor();
                break;

            case SymbolType.StaticField:
            case SymbolType.UnknownFunction:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (ret is null)
        {
            return ret;
        }

        CustomAttribute attr = new(module.ImportReference(typeof(SymbolAttribute).GetConstructors().First()));
        attr.ConstructorArguments.Add(new(Utils.String, t.Symbol));
        ret.CustomAttributes.Add(attr);

        attr = new(module.ImportReference(typeof(RVAAttribute).GetConstructors().First()));
        attr.ConstructorArguments.Add(new(module.ImportReference(typeof(ulong)), t.RVA));
        ret.CustomAttributes.Add(attr);

        return ret;
    }

    private MethodDefinition BuildExtensionFunction(
        ItemAccessType itemAccessType,
        PropertyDefinition fptrProperty,
        IMethodSignature functionPointer,
        bool isVarArg,
        Type extensionType,
        in Item t)
    {
        if (extensionType.IsValueType)
        {
            return BuildExtensionFunctionValueType(itemAccessType, fptrProperty, functionPointer, isVarArg,
                extensionType, t);
        }

        bool hasThis = Utils.HasThis(itemAccessType);

        (int begin, int end) loopRange = (1, functionPointer.Parameters.Count);
        if (isVarArg)
        {
            loopRange.end -= 1;
        }

        string methodName = t.Name.Contains("operator") ? Utils.SelectOperatorName(t) :
            t.Name.Length > 1 ? $"{char.ToUpper(t.Name[0])}{t.Name[1..]}" : t.Name.ToUpper();

        MethodDefinition method = new(methodName, MethodAttributes.Public | MethodAttributes.Static,
            functionPointer.ReturnType);
        if (isVarArg)
        {
            method.CallingConvention |= MethodCallingConvention.VarArg;
        }

        if (hasThis)
        {
            method.Parameters.Add(new("this", ParameterAttributes.None, module.ImportReference(extensionType)));
            CustomAttribute attr = new(module.ImportReference(typeof(ExtensionAttribute).GetConstructors().First()));
            method.CustomAttributes.Add(attr);
        }

        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            method.Parameters.Add(new(Utils.GetParameterName(t, true, i), functionPointer.Parameters[i].Attributes,
                functionPointer.Parameters[i].ParameterType));
        }


        ILProcessor? il = method.Body.GetILProcessor();

        il.Emit(OC.Ldarg_0);
        il.Emit(OC.Callvirt,
            module.ImportReference(extensionType.GetProperty(nameof(ICppInstanceNonGeneric.Pointer))!.GetMethod));

        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            il.Emit(OC.Ldarg_S, method.Parameters[i]);
        }

        if (isVarArg)
        {
            il.Emit(OC.Arglist);
        }

        CallSite callSite = new(module.ImportReference(functionPointer.ReturnType))
        {
            CallingConvention = MethodCallingConvention.Unmanaged
        };
        foreach (ParameterDefinition? param in functionPointer.Parameters)
        {
            callSite.Parameters.Add(param);
        }

        il.Emit(OC.Call, fptrProperty.GetMethod);
        il.Emit(OC.Calli, callSite);

        il.Emit(OC.Ret);
        return method;
    }

    private MethodDefinition BuildExtensionFunctionValueType(
        ItemAccessType itemAccessType,
        PropertyDefinition fptrProperty,
        IMethodSignature functionPointer,
        bool isVarArg,
        Type extensionType,
        in Item t)
    {
        bool hasThis = Utils.HasThis(itemAccessType);

        (int begin, int end) loopRange = (0, functionPointer.Parameters.Count);
        if (hasThis)
        {
            loopRange.begin += 1;
        }

        if (isVarArg)
        {
            loopRange.end -= 1;
        }

        string methodName = t.Name.Contains("operator") ? Utils.SelectOperatorName(t) :
            t.Name.Length > 1 ? $"{char.ToUpper(t.Name[0])}{t.Name[1..]}" : t.Name.ToUpper();

        MethodDefinition method = new(methodName,
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static, functionPointer.ReturnType);
        if (isVarArg)
        {
            method.CallingConvention |= MethodCallingConvention.VarArg;
        }

        if (hasThis)
        {
            ParameterDefinition param = new("this", ParameterAttributes.In,
                module.ImportReference(extensionType).MakeByReferenceType());
            CustomAttribute paramIsReadOnlyAttr =
                new(module.ImportReference(typeof(IsReadOnlyAttribute).GetConstructors().First()));
            param.CustomAttributes.Add(paramIsReadOnlyAttr);
            method.Parameters.Add(param);

            CustomAttribute attr = new(module.ImportReference(typeof(ExtensionAttribute).GetConstructors().First()));
            method.CustomAttributes.Add(attr);
        }

        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            method.Parameters.Add(new(Utils.GetParameterName(t, false, i), functionPointer.Parameters[i].Attributes,
                functionPointer.Parameters[i].ParameterType));
        }

        ILProcessor? il = method.Body.GetILProcessor();

        if (hasThis)
        {
            il.Emit(OC.Ldarg_0);
        }

        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            il.Emit(OC.Ldarg_S, method.Parameters[i]);
        }

        if (isVarArg)
        {
            il.Emit(OC.Arglist);
        }

        CallSite callSite = new(module.ImportReference(functionPointer.ReturnType))
        {
            CallingConvention = MethodCallingConvention.Unmanaged
        };
        foreach (ParameterDefinition? param in functionPointer.Parameters)
        {
            callSite.Parameters.Add(param);
        }

        il.Emit(OC.Call, fptrProperty.GetMethod);
        il.Emit(OC.Calli, callSite);

        il.Emit(OC.Ret);

        return method;
    }


    private MethodDefinition BuildExtensionCtor(PropertyDefinition fptrProperty,
        IMethodSignature functionPointer,
        bool isVarArg,
        Type extensionType,
        in Item t)
    {
        if (extensionType.IsValueType)
        {
            return BuildExtensionCtorValueType(fptrProperty, functionPointer, isVarArg, extensionType, t);
        }

        int classSize = extensionType.GetProperty(nameof(ICppInstanceNonGeneric.ClassSize))!.GetValue(null) as int? ??
                        0;

        (int begin, int end) loopRange = (1, functionPointer.Parameters.Count);
        if (isVarArg)
        {
            loopRange.end -= 1;
        }

        MethodDefinition method = new("CreateInstance",
            MethodAttributes.Public | MethodAttributes.Static, module.ImportReference(extensionType));
        if (isVarArg)
        {
            method.CallingConvention |= MethodCallingConvention.VarArg;
        }


        if (classSize is 0)
        {
            method.Parameters.Add(new("allocSize", ParameterAttributes.None, module.ImportReference(typeof(ulong))));
        }

        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            ParameterDefinition param = functionPointer.Parameters[i];
            method.Parameters.Add(
                new(Utils.GetParameterName(t, true, i), ParameterAttributes.None, param.ParameterType));
        }

        VariableDefinition ptr = new(module.ImportReference(typeof(void).MakePointerType()));
        method.Body.Variables.Add(ptr);

        ILProcessor? il = method.Body.GetILProcessor();
        if (classSize is 0)
        {
            il.Emit(OC.Ldarg_0);
        }
        else
        {
            il.Emit(OC.Call,
                module.ImportReference(extensionType.GetProperty(nameof(ICppInstanceNonGeneric.ClassSize))!
                    .GetMethod));
        }

        il.Emit(OC.Call, module.ImportReference(typeof(HeapAlloc).GetMethod(nameof(HeapAlloc.New))));
        il.Emit(OC.Stloc, ptr);

        il.Emit(OC.Ldloc, ptr);
        for (int i = classSize is 0 ? 1 : 0; i < (method.Parameters.Count - (isVarArg ? 1 : 0)); i++)
        {
            il.Emit(OC.Ldarg_S, method.Parameters[i]);
        }

        if (isVarArg)
        {
            il.Emit(OC.Arglist);
        }

        CallSite callSite = new(module.ImportReference(module.ImportReference(typeof(void))))
        {
            CallingConvention = MethodCallingConvention.Unmanaged
        };
        foreach (ParameterDefinition? param in functionPointer.Parameters)
        {
            callSite.Parameters.Add(param);
        }

        il.Emit(OC.Call, fptrProperty.GetMethod);
        il.Emit(OC.Calli, callSite);

        il.Emit(OC.Ldloc, ptr);
        il.Emit(OC.Ldc_I4_1);
        il.Emit(OC.Ldc_I4_0);
        il.Emit(OC.Call, module.ImportReference(
            extensionType
                .GetMethods()
                .First(t => t is { Name: nameof(ICppInstanceNonGeneric.ConstructInstance), IsGenericMethod: true } &&
                            t.GetParameters().Length is 3)));
        il.Emit(OC.Ret);

        return method;
    }

    private MethodDefinition BuildExtensionCtorValueType(PropertyDefinition fptrProperty,
        IMethodSignature functionPointer,
        bool isVarArg,
        Type extensionType,
        in Item t)
    {
        (int begin, int end) loopRange = (1 /*@this*/, functionPointer.Parameters.Count);
        if (isVarArg)
        {
            loopRange.end -= 1;
        }

        MethodDefinition method = new("CreateInstance",
            MethodAttributes.Public | MethodAttributes.Static, module.ImportReference(extensionType));
        if (isVarArg)
        {
            method.CallingConvention |= MethodCallingConvention.VarArg;
        }

        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            ParameterDefinition param = functionPointer.Parameters[i];
            method.Parameters.Add(
                new(Utils.GetParameterName(t, true, i), ParameterAttributes.None, param.ParameterType));
        }

        VariableDefinition temp = new(module.ImportReference(extensionType));
        method.Body.Variables.Add(temp);

        ILProcessor? il = method.Body.GetILProcessor();

        il.Emit(OC.Ldloca_S, temp);

        for (int i = 0; i < (method.Parameters.Count - (isVarArg ? 1 : 0)); i++)
        {
            il.Emit(OC.Ldarg_S, method.Parameters[i]);
        }

        if (isVarArg)
        {
            il.Emit(OC.Arglist);
        }

        CallSite callSite = new(module.ImportReference(module.ImportReference(typeof(void))))
        {
            CallingConvention = MethodCallingConvention.Unmanaged
        };
        foreach (ParameterDefinition? param in functionPointer.Parameters)
        {
            callSite.Parameters.Add(param);
        }

        il.Emit(OC.Call, fptrProperty.GetMethod);
        il.Emit(OC.Calli, callSite);

        il.Emit(OC.Ldloc, temp);
        il.Emit(OC.Ret);

        return method;
    }


    public MethodDefinition? BuildExtensionVirtualMethod(
        FunctionPointerType functionPointer,
        bool isVarArg,
        Type extensionType,
        int virtIndex,
        in Item t,
        Action ifIsDtor)
    {
        if ((SymbolType)t.SymbolType is SymbolType.Destructor)
        {
            ifIsDtor();
            return null;
        }

        if (extensionType.IsValueType)
        {
            return BuildExtensionVirtualMethodValueType(functionPointer, isVarArg, virtIndex, t);
        }

        (int begin, int end) loopRange = (1, functionPointer.Parameters.Count);
        if (isVarArg)
        {
            loopRange.end -= 1;
        }

        string methodName = t.Name.Contains("operator") ? Utils.SelectOperatorName(t) :
            t.Name.Length > 1 ? $"{char.ToUpper(t.Name[0])}{t.Name[1..]}" : t.Name.ToUpper();

        MethodDefinition method = new(methodName, MethodAttributes.Public | MethodAttributes.Static,
            functionPointer.ReturnType);
        if (isVarArg)
        {
            method.CallingConvention |= MethodCallingConvention.VarArg;
        }


        method.Parameters.Add(new("this", ParameterAttributes.None, module.ImportReference(extensionType)));
        CustomAttribute attr = new(module.ImportReference(typeof(ExtensionAttribute).GetConstructors().First()));
        method.CustomAttributes.Add(attr);


        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            method.Parameters.Add(new(Utils.GetParameterName(t, true, i), functionPointer.Parameters[i].Attributes,
                functionPointer.Parameters[i].ParameterType));
        }

        VariableDefinition fptr = new(module.ImportReference(typeof(void).MakePointerType()));
        method.Body.Variables.Add(fptr);
        ILProcessor? il = method.Body.GetILProcessor();

        il.Emit(OC.Ldarg_0);
        il.Emit(OC.Callvirt,
            module.ImportReference(extensionType.GetProperty(nameof(ICppInstanceNonGeneric.Pointer))!.GetMethod));
        il.Emit(OC.Ldc_I4, virtIndex);
        il.Emit(OC.Call, module.ImportReference(typeof(CppTypeSystem)
            .GetMethods()
            .First(f => f is
            { Name: nameof(CppTypeSystem.GetVurtualFunctionPointerByIndex), IsGenericMethodDefinition: false })));
        il.Emit(OC.Stloc, fptr);


        il.Emit(OC.Ldarg_0);
        il.Emit(OC.Callvirt,
            module.ImportReference(extensionType.GetProperty(nameof(ICppInstanceNonGeneric.Pointer))!.GetMethod));

        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            il.Emit(OC.Ldarg_S, method.Parameters[i]);
        }

        if (isVarArg)
        {
            il.Emit(OC.Arglist);
        }

        CallSite callSite = new(module.ImportReference(functionPointer.ReturnType))
        {
            CallingConvention = MethodCallingConvention.Unmanaged
        };
        foreach (ParameterDefinition? param in functionPointer.Parameters)
        {
            callSite.Parameters.Add(param);
        }

        il.Emit(OC.Ldloc, fptr);
        il.Emit(OC.Calli, callSite);

        il.Emit(OC.Ret);
        return method;
    }

    private MethodDefinition BuildExtensionVirtualMethodValueType(
        IMethodSignature functionPointer,
        bool isVarArg,
        int virtIndex,
        in Item t)
    {
        (int begin, int end) loopRange = (0, functionPointer.Parameters.Count);
        if (isVarArg)
        {
            loopRange.end -= 1;
        }

        string methodName = t.Name.Contains("operator") ? Utils.SelectOperatorName(t) :
            t.Name.Length > 1 ? $"{char.ToUpper(t.Name[0])}{t.Name[1..]}" : t.Name.ToUpper();

        MethodDefinition method = new(methodName, MethodAttributes.Public | MethodAttributes.Static,
            functionPointer.ReturnType);
        if (isVarArg)
        {
            method.CallingConvention |= MethodCallingConvention.VarArg;
        }

        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            method.Parameters.Add(new(Utils.GetParameterName(t, false, i), functionPointer.Parameters[i].Attributes,
                functionPointer.Parameters[i].ParameterType));
        }

        CustomAttribute attr = new(module.ImportReference(typeof(ExtensionAttribute).GetConstructors().First()));
        method.CustomAttributes.Add(attr);
        method.Parameters[0].Attributes |= ParameterAttributes.In;

        VariableDefinition fptr = new(module.ImportReference(typeof(void).MakePointerType()));
        method.Body.Variables.Add(fptr);
        ILProcessor? il = method.Body.GetILProcessor();

        il.Emit(OC.Ldarga_S, 0);
        il.Emit(OC.Conv_I);
        il.Emit(OC.Ldc_I4, virtIndex);
        il.Emit(OC.Call, module.ImportReference(typeof(CppTypeSystem)
            .GetMethods()
            .First(f => f is
            { Name: nameof(CppTypeSystem.GetVurtualFunctionPointerByIndex), IsGenericMethodDefinition: false })));
        il.Emit(OC.Stloc, fptr);

        for (int i = loopRange.begin; i < loopRange.end; i++)
        {
            il.Emit(OC.Ldarg_S, method.Parameters[i]);
        }

        if (isVarArg)
        {
            il.Emit(OC.Arglist);
        }

        CallSite callSite = new(module.ImportReference(functionPointer.ReturnType))
        {
            CallingConvention = MethodCallingConvention.Unmanaged
        };
        foreach (ParameterDefinition? param in functionPointer.Parameters)
        {
            callSite.Parameters.Add(param);
        }

        il.Emit(OC.Ldloc, fptr);
        il.Emit(OC.Calli, callSite);

        il.Emit(OC.Ret);

        return method;
    }
}