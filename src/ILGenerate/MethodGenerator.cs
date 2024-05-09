global using OC = Mono.Cecil.Cil.OpCodes;

using Hosihikari.Generation.CppParser;
using Hosihikari.Generation.Utils;
using Hosihikari.NativeInterop;
using Hosihikari.NativeInterop.Unmanaged;
using Hosihikari.NativeInterop.Unmanaged.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Hosihikari.Generation.ILGenerate;

public class MethodGenerator
{
    private enum PropertyMethodType
    {
        None = 0x0,
        Getter = 0x10,
        Setter = 0x20
    }


    public const string CtorPrefix = "ctor_";
    public const string DtorPrefix = "dtor_";
    public const string FuncPrefix = "func_";

    public const string FunctionPointerName = "FunctionPointer";

    public bool Generated { get; private set; }

    public TypeGenerator DeclaringType { get; }

    public AccessType AccessType { get; }

    public bool IsStatic { get; }

    public OriginalItem MethodItem { get; }

    public SymbolType SymbolType => (SymbolType)MethodItem.SymbolType;

    public CppType? ReturnType { get; }
    public CppType[] Parameters { get; }

    public MethodDefinition? Method { get; private set; }

    public AssemblyGenerator Assembly => DeclaringType.Assembly;

    private MethodGenerator(AccessType accessType, bool isStatic, TypeGenerator typeGenerator, OriginalItem item, CppType? returnType, CppType[] parameters)
    {
        DeclaringType = typeGenerator;
        AccessType = accessType;
        IsStatic = isStatic;
        MethodItem = item;
        ReturnType = returnType;
        Parameters = parameters;
    }

    // This method tries to create a MethodGenerator based on the provided inputs.
    // If successful, it returns true and assigns the generated MethodGenerator to the out parameter.
    // If unsuccessful, it returns false.
    /// <param name="accessType">The access type of the method.</param>
    /// <param name="isStatic">Flag indicating if the method is static.</param>
    /// <param name="item">The original item used to generate the method.</param>
    /// <param name="typeGenerator">The type generator used in generating the method.</param>
    /// <param name="methodGenerator">The generated method if successful, otherwise null.</param>
    public static bool TryCreateMethodGenerator(
            AccessType accessType,
            bool isStatic,
            OriginalItem item,
            TypeGenerator typeGenerator,
            [NotNullWhen(true)] out MethodGenerator? methodGenerator)
    {
        methodGenerator = null;

        if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Symbol))
            return false;

        var symbolType = (SymbolType)item.SymbolType;
        CppType? returnType = null;

        if (symbolType is SymbolType.Function || symbolType is SymbolType.Operator)
            if (CppTypeParser.TryParse(item.Type.Name, out returnType) is false)
                return false;
            else if (symbolType is SymbolType.StaticField)
                throw new InvalidOperationException("Static fields are not supported.");

        CppType[] parameters = item.Parameters is null ? [] : new CppType[item.Parameters.Length];

        if (item.Parameters is not null)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if (CppTypeParser.TryParse(item.Parameters[i].Name, out var parameterType) is false)
                    return false;

                parameters[i] = parameterType;
            }
        }

        methodGenerator = new MethodGenerator(accessType, isStatic, typeGenerator, item, returnType, parameters);
        return true;
    }


    /// <summary>
    /// Generates a method asynchronously based on the specified return type and parameters.
    /// </summary>
    /// <returns>True if the method was generated successfully, false otherwise.</returns>
    [MemberNotNullWhen(true, nameof(Method))]
    public async ValueTask<bool> GenerateAsync()
    {
        if (Generated) return Method is not null;
        Generated = true;


        var fptrField = await GenerateFunctionPointer(); // Generate function pointer if types are not resolved

        switch (SymbolType)
        {
            case SymbolType.Function:
                return await GenerateMethodAsync(fptrField);
            case SymbolType.Constructor:
                return await GenerateConstructorAsync(fptrField);
            case SymbolType.Destructor:
                return await GenerateDestructorAsync(fptrField);
            case SymbolType.Operator:
            case SymbolType.StaticField:
            case SymbolType.UnknownFunction:
                return false;
        }

        return false;
    }

    public async ValueTask<FieldDefinition> GenerateFunctionPointer()
    {
        // Generate the function pointer asynchronously
        return await Task.Run(() =>
        {
            // Get the type registry and original type builder
            var registry = DeclaringType.Assembly.TypeRegistry;
            var definition = DeclaringType.FunctionPointerDefintionType;

            // Create a string builder for the function pointer name
            StringBuilder builder = new((SymbolType)MethodItem.SymbolType switch
            {
                SymbolType.Constructor => CtorPrefix,
                SymbolType.Destructor => DtorPrefix,
                _ => FuncPrefix
            });

            // Append the symbol characters to the builder
            foreach (var c in MethodItem.Symbol)
            {
                builder.Append(char.IsLetter(c) || char.IsDigit(c) ? c : '_');
            }

            // Define the nested type for the function pointer
            var fptrType = definition.DefineType(
                string.Empty,
                builder.ToString(),
                TypeAttributes.NestedPublic |
                TypeAttributes.Sealed |
                TypeAttributes.Abstract |
                TypeAttributes.Class);

            // Define the field for the function pointer
            var field = fptrType.DefineField(
                FunctionPointerName,
                FieldAttributes.Public |
                FieldAttributes.Static |
                FieldAttributes.InitOnly,
                Assembly.ImportRef(typeof(nint)));

            // Define the constructor for the type
            var cctor = fptrType.DefineMethod(
                name: ".cctor",
                MethodAttributes.Static |
                MethodAttributes.Private |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName,
                returnType: Assembly.ImportRef(typeof(void)));

            // Get the ILGenerator for the constructor
            var il = cctor.Body.GetILProcessor();

            // Emit IL instructions to initialize the field with the symbol value
            il.Emit(OC.Ldstr, MethodItem.Symbol);
            il.Emit(OC.Call, Assembly.ImportRef(typeof(SymbolHelper).GetMethod(nameof(SymbolHelper.DlsymPointer))!));
            il.Emit(OC.Stsfld, field);
            il.Emit(OC.Ret);

            // Return the field builder
            return field;
        });
    }

    private static List<string> GetterPrefix { get; } = ["get", "is", "has", "can", "should"];

    private static List<string> SetterPrefix { get; } = ["set"];

    private bool TestPropertyMethod([NotNullWhen(true)] out bool? isGetter, [NotNullWhen(true)] out string? propertyName)
    {
        var name = MethodItem.Name.Length > 1 ? $"{char.ToLower(MethodItem.Name[0])}{MethodItem.Name[1..]}" : MethodItem.Name.ToLower();

        if (Parameters.Length is 0)
            foreach (var prefix in GetterPrefix)
            {
                if (name.StartsWith(prefix) && name.Length > prefix.Length)
                {
                    isGetter = true;
                    propertyName = name[prefix.Length..];
                    return true;
                }
            }

        if (Parameters.Length is 1 && ReturnType?.FundamentalType is CppFundamentalType.Void)
            foreach (var prefix in SetterPrefix)
            {
                if (name.StartsWith(prefix) && name.Length > prefix.Length)
                {
                    isGetter = false;
                    propertyName = name[prefix.Length..];
                    return true;
                }
            }

        isGetter = null;
        propertyName = null;
        return false;
    }

    public async ValueTask<bool> GenerateMethodAsync(FieldDefinition fptrField)
    {
        bool ret = false;
        await Task.Run(async () =>
        {
            var (success, returnType, parameterTypes, hasVarArgs) = await CheckTypes();
            if (success is false)
            {
                ret = false;
                return;
            };

            var method = GenerateOriginalMethod(returnType!, parameterTypes!, fptrField, hasVarArgs);
            ret = GenerateMethod(method, hasVarArgs);
        });

        return ret;
    }

    public async ValueTask<(bool success, TypeReference? returnType, TypeReference[]? parameterTypes, bool hasVarArgs)> CheckTypes()
    {
        if (SymbolType is SymbolType.StaticField || SymbolType is SymbolType.UnknownFunction)
        {
            return (false, default, default, default);
        }

        // Get the type registry from the AssemblyGenerator
        var registry = DeclaringType.Assembly.TypeRegistry;

        bool hasVarArgs = false;

        // Resolve the return type asynchronously
        TypeReference? returnType = null;
        if (SymbolType is SymbolType.Function)
            returnType = await registry.ResolveTypeAsync(ReturnType!);

        // Resolve parameter types asynchronously
        TypeReference?[] parameterTypes = new TypeReference[Parameters.Length + (IsStatic ? 0 : 1)];
        if (IsStatic is false) parameterTypes[0] = Assembly.ImportRef(typeof(nint));
        for (int i = 0; i < Parameters.Length; i++)
        {
            var parameter = Parameters[i];
            if (parameter.Type is CppTypeEnum.VarArgs)
            {
                hasVarArgs = true;
                continue;
            }

            var temp = await registry.ResolveTypeAsync(parameter);
            parameterTypes[i + (IsStatic ? 0 : 1)] = temp;
        }

        if (hasVarArgs)
            parameterTypes = parameterTypes[..^1];

        // Check if the return type or any parameter type is null
        if ((returnType is null && SymbolType is SymbolType.Function) || parameterTypes.Any(x => x is null))
        {
            return (false, default, default, default);
        }

        return (true, returnType, parameterTypes, hasVarArgs)!;
    }

    private bool GenerateMethod(MethodReference original, bool hasVarArgs)
    {
        if (TestPropertyMethod(out var isGetter, out var propertyName))
        {
            PropertyDefinition property;
            var properties = from x in DeclaringType.Type.Properties
                             where x.Name == propertyName
                             select x;
            property = properties.Any() ? properties.First() : DeclaringType.Type.DefineProperty(propertyName, PropertyAttributes.None, original.ReturnType);

            //getter only or setter only or both but property type is not by reference
            if ((isGetter.Value && property.GetMethod is null) ||
                (isGetter.Value is false && property.GetMethod is null && property.SetMethod is null) ||
                (isGetter.Value is false && property.SetMethod is null && property.PropertyType.IsByReference is false))
            {
                if (hasVarArgs) return GenerateMethodDefault(original, hasVarArgs);

                return GeneratePropertyMethod(property, isGetter.Value, original);
            }
            else return GenerateMethodDefault(original, hasVarArgs);
        }
        else return GenerateMethodDefault(original, hasVarArgs);
    }

    private bool GeneratePropertyMethod(PropertyDefinition property, bool isGetter, MethodReference original)
    {
        if (isGetter)
        {
            var attributes = MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.NewSlot;
            if (IsStatic) attributes |= MethodAttributes.Static;

            var method = DeclaringType.Type.DefineMethod(
                $"get_{property.Name}",
                attributes,
                original.ReturnType);
            var il = method.Body.GetILProcessor();
            if (IsStatic is false)
            {
                il.LoadThis();
                il.Emit(OC.Call, DeclaringType.PointerProperty?.GetMethod ?? throw new Exception("pointer property is null"));
            }
            il.Emit(OC.Call, original);
            il.Emit(OC.Ret);

            property.BindMethods(getMethod: method);
        }
        else
        {
            var attributes = MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.NewSlot;
            if (IsStatic) attributes |= MethodAttributes.Static;

            var method = DeclaringType.Type.DefineMethod(
                $"set_{property.Name}",
                attributes,
                Assembly.ImportRef(typeof(void)),
                parameterTypes: [new("value", ParameterAttributes.None, property.PropertyType)]);
            var il = method.Body.GetILProcessor();
            if (IsStatic is false)
            {
                il.LoadThis();
                il.Emit(OC.Call, DeclaringType.PointerProperty!.GetMethod);
            }
            il.LoadAllArgs();
            il.Emit(OC.Call, original);
            il.Emit(OC.Ret);

            property.BindMethods(setMethod: method);
        }

        return true;
    }
    private bool GenerateMethodDefault(MethodReference original, bool hasVarArgs)
    {
        var attributes = MethodAttributes.Public;
        if (IsStatic) attributes |= MethodAttributes.Static;

        var method = DeclaringType.Type.DefineMethod(
            MethodItem.GetMethodNameUpper(),
            attributes,
            original.ReturnType,
            from x in IsStatic ? original.Parameters : original.Parameters.Skip(1)
            select x.Clone());
        if (hasVarArgs) method.CallingConvention |= MethodCallingConvention.VarArg;
        var il = method.Body.GetILProcessor();
        if (IsStatic is false)
        {
            il.LoadThis();
            il.Emit(OC.Call, DeclaringType.PointerProperty!.GetMethod);
        }
        il.LoadAllArgs();
        if (hasVarArgs)
        {
            il.Emit(OC.Arglist);
            var temp = Assembly.ImportRef(original);
            temp.CallingConvention ^= MethodCallingConvention.VarArg;
            temp.Parameters.Add(new(Assembly.ImportRef(typeof(RuntimeArgumentHandle))));
            il.Emit(OC.Call, temp);
        }
        else il.Emit(OC.Call, original);
        il.Emit(OC.Ret);

        return true;
    }

    public MethodDefinition GenerateOriginalMethod(
        TypeReference returnType,
        TypeReference[] parameterTypes,
        FieldReference fptrField,
        bool hasVarArgs,
        (int vtblOffset, int virtIndex)? virt = null)
    {
        /// <summary>
        /// Generates the original method name based on certain criteria.
        /// </summary>
        /// <returns>The generated original method name.</returns>
        string GenerateOriginalMethodName()
        {
            // Define a hash function to calculate the integer hash value
            static int hash(string str)
            {
                int rval = 0;
                for (int i = 0; i < str.Length; ++i)
                {
                    if ((i & 1) != 0)
                        rval ^= (~((rval << 11) ^ str[i] ^ (rval >> 5)));
                    else
                        rval ^= (~((rval << 7) ^ str[i] ^ (rval >> 3)));
                }
                return rval;
            }

            static char select(string str)
            {
                var val = (uint)hash(str) % 36;
                return (char)('0' + val switch
                {
                    >= 0 and < 10 => val,
                    >= 10 and < 36 => val + 7,
                    _ => '_' - '0'
                });
            }

            var temp = SelectOperatorName();
            // Generate the method name based on the MethodItem properties
            temp ??= MethodItem.GetMethodNameUpper();

            // Generate the method name using the hash of parameter names
            StringBuilder builder = new(temp);

            builder.Append($"_{MethodItem.Parameters?.Length ?? 0}");

            foreach (var param in MethodItem.Parameters ?? [])
            {
                builder.Append(select(param.Name));
            }
            builder.Append($"_{select(MethodItem.Type.Name)}");
            if (virt is not null) builder.Append("_Virt");

            return builder.ToString();
        }

        var original = DeclaringType.OriginalType;

        var originalMethod = original.DefineMethod(
            GenerateOriginalMethodName(),
            MethodAttributes.Public |
            MethodAttributes.Static,
            returnType,
            (from param in parameterTypes.Index()
             let paramNames = MethodItem.ParameterNames ?? []
             let paramName = param.Index < paramNames.Count ? paramNames[param.Index] : string.Empty
             select new ParameterDefinition(paramName == "/*this*/" ? "this" : paramName, ParameterAttributes.None, param.Item)).ToArray());

        if (hasVarArgs) originalMethod.CallingConvention |= MethodCallingConvention.VarArg;
        originalMethod.CustomAttributes.Add(
            new(Assembly.ImportRef(typeof(SymbolAttribute).GetConstructors().First()))
            {
                ConstructorArguments = { new(Assembly.ImportRef(typeof(string)), MethodItem.Symbol) }
            });
        originalMethod.CustomAttributes.Add(
            new(Assembly.ImportRef(typeof(RVAAttribute).GetConstructors().First()))
            {
                ConstructorArguments = { new(Assembly.ImportRef(typeof(ulong)), MethodItem.Rva) }
            });

        var il = originalMethod.Body.GetILProcessor();
        il.LoadAllArgs();
        if (hasVarArgs)
            il.Emit(OC.Arglist);
        il.Emit(OC.Ldsfld, fptrField);
        if (hasVarArgs) parameterTypes = [.. parameterTypes, Assembly.ImportRef(typeof(RuntimeArgumentHandle))];
        il.EmitCalli(
            MethodCallingConvention.C,
            returnType,
            CecilExtension.CreateParamDefinitions(parameterTypes!));
        il.Emit(OC.Ret);

        return originalMethod;
    }

    private async ValueTask<bool> GenerateConstructorAsync(FieldDefinition fptrField)
    {
        bool ret = false;
        await Task.Run(async () =>
        {
            var (success, returnType, parameterTypes, hasVarArgs) = await CheckTypes();
            if (success is false)
            {
                ret = false;
                return;
            };

            returnType = parameterTypes![0] = Assembly.ImportRef(typeof(nint));

            var method = GenerateOriginalMethod(returnType!, parameterTypes!, fptrField, hasVarArgs);
            ret = GenerateConstructorBody(method, hasVarArgs);
        });

        return ret;
    }

    private bool GenerateConstructorBody(MethodDefinition original, bool hasVarArgs)
    {
        var ctor = DeclaringType.Type.DefineMethod(
            ".ctor",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName,
            Assembly.ImportRef(typeof(void)),
            parameterTypes: (from param in original.Parameters.Skip(1)
                             select param.Clone())
                            .Prepend(new("allocSize", ParameterAttributes.None, Assembly.ImportRef(typeof(ulong)))));
        if (hasVarArgs) ctor.CallingConvention |= MethodCallingConvention.VarArg;
        ILProcessor il = ctor.Body.GetILProcessor();
        var fptr = new VariableDefinition(Assembly.ImportRef(typeof(void).MakePointerType()));
        ctor.Body.Variables.Add(fptr);
        il.LoadThis();
        il.Emit(OC.Call, Assembly.ImportRef(Assembly.TypeSystem.Object.GetConstructors().First()));
        il.Emit(OC.Ldarg_1);
        il.Emit(OC.Call, Assembly.ImportRef(typeof(NativeAlloc).GetMethod(nameof(NativeAlloc.New))!));
        il.Emit(OC.Stloc, fptr);
        il.Emit(OC.Ldloc, fptr);
        foreach (var (index, _) in ctor.Parameters.Index().Skip(1))
            il.Emit(OC.Ldarg, index + 1);
        if (hasVarArgs)
        {
            il.Emit(OC.Arglist);
            var temp = Assembly.ImportRef(original);
            temp.CallingConvention ^= MethodCallingConvention.VarArg;
            temp.Parameters.Add(new(Assembly.ImportRef(typeof(RuntimeArgumentHandle))));
            il.Emit(OC.Call, temp);
        }
        else il.Emit(OC.Call, original);
        il.Emit(OC.Pop);
        il.LoadThis();
        il.Emit(OC.Ldloc, fptr);
        il.Emit(OC.Call, DeclaringType.PointerProperty?.SetMethod ?? throw new Exception("pointer property is null"));
        il.LoadThis();
        il.Emit(OC.Ldc_I4_1);
        il.Emit(OC.Conv_I1);
        il.Emit(OC.Call, DeclaringType.IsOwnerProperty?.SetMethod ?? throw new Exception("owner property is null"));
        il.LoadThis();
        il.Emit(OC.Ldc_I4_1);
        il.Emit(OC.Conv_I1);
        il.Emit(OC.Call, DeclaringType.OwnsMemoryProperty?.SetMethod ?? throw new Exception("owner property is null"));
        il.Emit(OC.Ret);

        return true;
    }


    private async ValueTask<bool> GenerateDestructorAsync(FieldDefinition fptrField)
    {
        await Task.Run(async () =>
        {
            var original = GenerateOriginalMethod(Assembly.ImportRef(typeof(void)), [Assembly.ImportRef(typeof(nint))], fptrField, false);
            await DeclaringType.GenerateDtorDefinitionAsync(il =>
            {
                il.Emit(OC.Ldarg_0);
                il.Emit(OC.Call, original);
                il.Emit(OC.Ret);
            });
        });
        return true;
    }

    private string? SelectOperatorName()
    {
        //https://learn.microsoft.com/en-us/cpp/cpp/operator-overloading?view=msvc-170
        return MethodItem.Name switch
        {
            "operator," => "operator_Comma",
            "operator!" => "operator_LogicalNOT",
            "operator!=" => "operator_Inequality",
            "operator%" => "operator_Modulus",
            "operator%=" => "operator_ModulusAssignment",
            "operator&" => MethodItem.Parameters?.Length switch
            {
                null => null,
                1 => "operator_AddressOf",
                _ => "operator_BitwiseAND"
            },
            "operator&&" => "operator_LogicalAND",
            "operator&=" => "operator_BitwiseAND_Assignment",
            "operator()" => MethodItem.Parameters?.Length switch
            {
                null => null,
                1 => "operator_Cast",
                _ => "operator_FunctionCall",
            },
            "operator*" => MethodItem.Parameters?.Length switch
            {
                null => null,
                1 => "operator_PointerDereference",
                _ => "operator_Multiplication"
            },
            "operator*=" => "operator_MultiplicationAssignment",
            "operator+" => "operator_Addition",
            "operator++" => "operator_Increment",
            "operator+=" => "operator_AdditionAssignment",
            "operator-" => "operator_Subtraction",
            "operator--" => "operator_Decrement",
            "operator-=" => "operator_SubtractionAssignment",
            "operator->" => "operator_MemberSelection",
            "operator->*" => "operator_PointerToMemberSelection",
            "operator/" => "operator_Division",
            "operator/=" => "operator_DivisionAssignment",
            "operator<" => "operator_LessThan",
            "operator<<" => "operator_LeftShift",
            "operator<<=" => "operator_LeftShiftAssignment",
            "operator<=" => "operator_LessThanOrEqualTo",
            "operator=" => "operator_Assignment",
            "operator==" => "operator_Equality",
            "operator>" => "operator_GreaterThan",
            "operator>=" => "operator_GreaterThanOrEqualTo",
            "operator>>" => "operator_RightShift",
            "operator>>=" => "operator_RightShiftAssignment",
            "operator[]" => "operator_Array_subscript",
            "operator^" => "operator_ExclusiveOR",
            "operator^=" => "operator_ExclusiveOR_Assignment",
            "operator|" => "operator_BitwiseInclusiveOR",
            "operator|=" => "operator_BitwiseInclusiveOR_Assignment",
            "operator||" => "operator_LogicalOR",
            "operator new" => "operator_new",
            "operator delete" => "operator_delete",
            _ => null
        };
    }
}