global using OC = Mono.Cecil.Cil.OpCodes;

using Hosihikari.Generation.CppParser;
using Hosihikari.Generation.Utils;
using Hosihikari.NativeInterop;
using Hosihikari.NativeInterop.Unmanaged.Attributes;
using Mono.Cecil;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace Hosihikari.Generation.ILGenerate;

public class MethodGenerator
{
    public const string CtorPrefix = "ctor_";
    public const string DtorPrefix = "dtor_";
    public const string FuncPrefix = "func_";

    public const string FunctionPointerName = "FunctionPointer";

    public TypeGenerator DeclaringType { get; }

    public AccessType AccessType { get; }

    public bool IsStatic { get; }

    public OriginalItem MethodItem { get; }

    public SymbolType SymbolType => (SymbolType)MethodItem.SymbolType;

    public CppType? ReturnType { get; }
    public CppType[] Parameters { get; }

    public MethodDefinition? Method { get; private set; }

    private AssemblyGenerator Assembly => DeclaringType.Assembly;

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

        var fptrField = await GenerateFunctionPointer(); // Generate function pointer if types are not resolved

        switch (SymbolType)
        {
            case SymbolType.Function:
                return await GenerateMethodAsync(fptrField);
            case SymbolType.Constructor:
            case SymbolType.Destructor:
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

    public async ValueTask<bool> GenerateMethodAsync(FieldReference fptrField)
    {
        bool ret = false;
        await Task.Run(async () =>
        {
            if (SymbolType is not SymbolType.Function)
            {
                ret = false;
                return;
            }

            // Get the type registry from the AssemblyGenerator
            var registry = DeclaringType.Assembly.TypeRegistry;

            bool hasVarArgs = false;

            // Resolve the return type asynchronously
            TypeReference? returnType = await registry.ResolveTypeAsync(ReturnType!);

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

            // Check if the return type or any parameter type is null
            if (returnType is null || parameterTypes.Any(x => x is null))
            {
                ret = false;
                return;
            }

            var method = GenerateOriginalMethod(returnType, parameterTypes!, fptrField, hasVarArgs);
        });

        return ret;
    }

    private MethodDefinition GenerateOriginalMethod(
        TypeReference returnType,
        TypeReference[] parameterTypes,
        FieldReference fptrField,
        bool hasVarArgs)
    {
        var original = DeclaringType.OriginalType;

        var originalMethod = original.DefineMethod(
            GenerateOriginalMethodName(),
            MethodAttributes.Public |
            MethodAttributes.Static,
            returnType,
            parameterTypes!);
        if (hasVarArgs) originalMethod.CallingConvention = MethodCallingConvention.VarArg;
        originalMethod.CustomAttributes.Add(
            new(Assembly.ImportRef(typeof(SymbolAttribute).GetConstructors().First()))
            {
                ConstructorArguments =
                {
                    new(Assembly.ImportRef(typeof(string)), MethodItem.Symbol)
                }
            });
        originalMethod.CustomAttributes.Add(
            new(Assembly.ImportRef(typeof(RVAAttribute).GetConstructors().First()))
            {
                ConstructorArguments =
                {
                    new(Assembly.ImportRef(typeof(ulong)), MethodItem.Rva)
                }
            });

        var il = originalMethod.Body.GetILProcessor();
        il.LoadAllArgs();
        il.Emit(OC.Ldsfld, fptrField);
        il.EmitCalli(
            MethodCallingConvention.C,
            returnType,
            CecilExtension.CreateParamDefinitions(parameterTypes!));
        il.Emit(OC.Ret);

        return originalMethod;
    }


    /// <summary>
    /// Generates the original method name based on certain criteria.
    /// </summary>
    /// <returns>The generated original method name.</returns>
    private string GenerateOriginalMethodName()
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

        var temp = SelectOperatorName();
        // Generate the method name based on the MethodItem properties
        temp ??= MethodItem.Name.Length > 1 ? $"{char.ToUpper(MethodItem.Name[0])}{MethodItem.Name[1..]}" : MethodItem.Name.ToUpper();

        if (MethodItem.Parameters is null)
            return temp;

        // Generate the method name using the hash of parameter names
        StringBuilder builder = new(temp);

        builder.Append($"_{MethodItem.Parameters.Length}");

        foreach (var param in MethodItem.Parameters)
        {
            var val = (uint)hash(param.Name) % 36;
            builder.Append((char)('0' + val switch
            {
                >= 0 and < 10 => val,
                >= 10 and < 36 => val + 8,
                _ => '_' - '0'
            }));
        }

        return builder.ToString();
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