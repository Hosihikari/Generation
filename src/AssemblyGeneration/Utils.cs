using System.Text;

using Hosihikari.NativeInterop.Unmanaged;
using Hosihikari.Utils;
using Hosihikari.Generation.Generator;

using Mono.Cecil;
using Mono.Cecil.Rocks;

using static Hosihikari.Utils.OriginalData.Class;
using static Hosihikari.Generation.AssemblyGeneration.AssemblyBuilder;


namespace Hosihikari.Generation.AssemblyGeneration;

public static class Utils
{
    public static string SelectOperatorName(in Item t)
    {
        //https://learn.microsoft.com/en-us/cpp/cpp/operator-overloading?view=msvc-170
        return t.Name switch
        {
            "operator," => "operator_Comma",
            "operator!" => "operator_Logical_NOT",
            "operator!=" => "operator_Inequality",
            "operator%" => "operator_Modulus",
            "operator%=" => "operator_Modulus_assignment",
            "operator&" => t.Params.Count is 1 ? "operator_Address_of" : "operator_Bitwise_AND",
            "operator&&" => "operator_Logical_AND",
            "operator&=" => "operator_Bitwise_AND_assignment",
            "operator()" => t.Params.Count is 1 ? "operator_Cast" : "operator_Function_call",
            "operator*" => t.Params.Count is 1 ? "operator_Pointer_dereference" : "operator_Multiplication",
            "operator*=" => "operator_Multiplication_assignment",
            "operator+" => "operator_Addition",
            "operator++" => "operator_Increment",
            "operator+=" => "operator_Addition_assignment",
            "operator-" => "operator_Subtraction",
            "operator--" => "operator_Decrement",
            "operator-=" => "operator_Subtraction_assignment",
            "operator->" => "operator_Member_selection",
            "operator->*" => "operator_Pointer_to_member_selection",
            "operator/" => "operator_Division",
            "operator/=" => "operator_Division_assignment",
            "operator<" => "operator_Less_than",
            "operator<<" => "operator_Left_shift",
            "operator<<=" => "operator_Left_shift_assignment",
            "operator<=" => "operator_Less_than_or_equal_to",
            "operator=" => "operator_Assignment",
            "operator==" => "operator_Equality",
            "operator>" => "operator_Greater_than",
            "operator>=" => "operator_Greater_than_or_equal_to",
            "operator>>" => "operator_Right_shift",
            "operator>>=" => "operator_Right_shift_assignment",
            "operator[]" => "operator_Array_subscript",
            "operator^" => "operator_Exclusive_OR",
            "operator^=" => "operator_Exclusive_OR_assignment",
            "operator|" => "operator_Bitwise_inclusive_OR",
            "operator|=" => "operator_Bitwise_inclusive_OR_assignment",
            "operator||" => "operator_Logical_OR",
            "operator new" => "operator_new",
            "operator delete" => "operator_delete",
            _ => throw new InvalidDataException()
        };
    }

    public static bool HasThis(ItemAccessType accessType) =>
    accessType is ItemAccessType.Public ||
    accessType is ItemAccessType.Protected ||
    accessType is ItemAccessType.Virtual ||
    accessType is ItemAccessType.VirtualUnordered;

    public static string BuildFptrId(in Item t)
    {
        StringBuilder builder = new();
        var prefix = (SymbolType)t.SymbolType switch
        {
            SymbolType.Constructor => "ctor_",
            SymbolType.Destructor => "dtor_",
            _ => string.Empty
        };

        builder.Append(prefix);
        foreach (var c in t.Symbol)
            builder.Append(TypeAnalyzer.IsLetterOrUnderline(c) ? c : '_');
        return builder.ToString();
    }

    public static string BuildFptrName(HashSet<string> fptrFieldNames, in Item t, Random random)
    {
        var fptrName = t.Name;
        if (fptrFieldNames.Contains(fptrName))
        {
            if (t.Params is null)
                return $"{fptrName}_Overload{random.NextInt64()}";

            StringBuilder builder = new(fptrName);
            foreach (var param in t.Params)
            {
                builder.Append('_');
                foreach (var c in param.Name)
                    builder.Append(TypeAnalyzer.IsLetterOrUnderline(c) ? c : '_');
            }
            return builder.ToString();
        }

        if (fptrName.Contains("operator"))
            fptrName = $"cpp_{SelectOperatorName(t)}";

        if ((SymbolType)t.SymbolType is SymbolType.Destructor)
            fptrName = $"destructor_{fptrName[1..]}";
        if ((SymbolType)t.SymbolType is SymbolType.Constructor)
            fptrName = $"constructor_{fptrName}";

        return fptrName;
    }

    public static (TypeReference @ref, string name) BuildReference(
        Dictionary<string, TypeDefinition> definedTypes,
        ModuleDefinition module,
        in TypeData type,
        bool isResult = false)
    {
        var arr = type.Analyzer.CppTypeHandle.ToArray().Reverse();

        TypeReference? reference = null;
        bool isUnmanagedType = false;
        bool rootTypeParsed = false;

        foreach (var item in arr)
        {

            switch (item.Type)
            {
                case CppTypeEnum.FundamentalType:
                    if (rootTypeParsed is true)
                        throw new InvalidOperationException();

                    switch (item.FundamentalType!)
                    {
                        case CppFundamentalType.Void:
                            reference = module.TypeSystem.Void; goto UNMANAGED_TYPE_EQUALS_TRUE;
                        case CppFundamentalType.Boolean:
                            reference = module.TypeSystem.Boolean; goto UNMANAGED_TYPE_EQUALS_TRUE;
                        case CppFundamentalType.Float:
                            reference = module.TypeSystem.Single; goto UNMANAGED_TYPE_EQUALS_TRUE;
                        case CppFundamentalType.Double:
                            reference = module.TypeSystem.Double; goto UNMANAGED_TYPE_EQUALS_TRUE;
                        case CppFundamentalType.WChar:
                            reference = module.TypeSystem.Char; goto UNMANAGED_TYPE_EQUALS_TRUE;
                        case CppFundamentalType.SChar:
                            reference = module.TypeSystem.SByte; goto UNMANAGED_TYPE_EQUALS_TRUE;
                        case CppFundamentalType.Int16:
                            reference = module.TypeSystem.Int16; goto UNMANAGED_TYPE_EQUALS_TRUE;
                        case CppFundamentalType.Int32:
                            reference = module.TypeSystem.Int32; goto UNMANAGED_TYPE_EQUALS_TRUE;
                        case CppFundamentalType.Int64:
                            reference = module.TypeSystem.Int64; goto UNMANAGED_TYPE_EQUALS_TRUE;
                        case CppFundamentalType.Char:
                            reference = module.TypeSystem.Byte; goto UNMANAGED_TYPE_EQUALS_TRUE;
                        case CppFundamentalType.UInt16:
                            reference = module.TypeSystem.UInt16; goto UNMANAGED_TYPE_EQUALS_TRUE;
                        case CppFundamentalType.UInt32:
                            reference = module.TypeSystem.UInt32; goto UNMANAGED_TYPE_EQUALS_TRUE;
                        case CppFundamentalType.UInt64:
                            reference = module.TypeSystem.UInt64; goto UNMANAGED_TYPE_EQUALS_TRUE;

                        UNMANAGED_TYPE_EQUALS_TRUE:
                            isUnmanagedType = true;
                            rootTypeParsed = true;
                            break;
                    }
                    break;

                case CppTypeEnum.Enum:
                    if (rootTypeParsed is true)
                        throw new InvalidOperationException();

                    reference = module.TypeSystem.Int32;
                    isUnmanagedType = true;
                    break;

                case CppTypeEnum.Array:
                case CppTypeEnum.Pointer:
                    if (isUnmanagedType)
                        reference = reference.MakePointerType();
                    else
                    {
                        var pointerType = new GenericInstanceType(module.ImportReference(typeof(Pointer<>)));
                        pointerType.GenericArguments.Add(reference);
                        reference = module.ImportReference(pointerType);
                        isUnmanagedType = true;
                    }
                    break;

                case CppTypeEnum.RValueRef:
                    if (isUnmanagedType)
                        return (reference.MakeByReferenceType(), "rvalRef");
                    else
                    {
                        var rvalRefType = new GenericInstanceType(module.ImportReference(typeof(RValueReference<>)));
                        rvalRefType.GenericArguments.Add(reference);
                        return (module.ImportReference(rvalRefType), string.Empty);
                    }

                case CppTypeEnum.Ref:
                    if (isUnmanagedType)
                        return (reference.MakeByReferenceType(), string.Empty);
                    else
                    {
                        var refType = new GenericInstanceType(module.ImportReference(typeof(Reference<>)));
                        refType.GenericArguments.Add(reference);
                        return (module.ImportReference(refType), string.Empty);
                    }

                case CppTypeEnum.Class:
                case CppTypeEnum.Struct:
                case CppTypeEnum.Union:
                    if (rootTypeParsed is true)
                        throw new InvalidOperationException();
                    {
                        reference = definedTypes[type.FullTypeIdentifier];
                        rootTypeParsed = true;
                    }
                    break;
            }
        }

        if (rootTypeParsed && isUnmanagedType is false)
        {
            if (isResult)
            {
                var rltType = new GenericInstanceType(module.ImportReference(typeof(Result<>)));
                rltType.GenericArguments.Add(reference);
                reference = module.ImportReference(rltType);
            }
            else
            {
                var refType = new GenericInstanceType(module.ImportReference(typeof(Reference<>)));
                refType.GenericArguments.Add(reference);
                reference = module.ImportReference(refType);
            }
        }

        return (reference!, string.Empty);
    }

    public static FunctionPointerType BuildFunctionPointerType(
        ModuleDefinition module,
        Dictionary<string, TypeDefinition> definedTypes,
        ItemAccessType itemAccessType,
        in Item t)
    {
        var fptrType = new FunctionPointerType { CallingConvention = MethodCallingConvention.Unmanaged };
        var typeData = (SymbolType)t.SymbolType switch
        {
            SymbolType.Constructor or SymbolType.Destructor => new TypeData(new() { Name = "void" }),
            _ => new TypeData(t.Type)
        };

        var (@ref, _) = BuildReference(definedTypes, module, typeData, true);
        fptrType.ReturnType = @ref;

        if (HasThis(itemAccessType))
            fptrType.Parameters.Add(new(module.TypeSystem.IntPtr));

        if (t.Params is null)
            return fptrType;
        else
        {
            foreach (var param in t.Params)
            {
                var type = new TypeData(param);
                if (type.Analyzer.CppTypeHandle.Type is CppTypeEnum.VarArgs)
                {
                    fptrType.Parameters.Add(new("args", ParameterAttributes.None, new SentinelType(module.TypeSystem.Object)));
                    continue;
                }

                var (reference, _) = BuildReference(definedTypes, module, type);
                fptrType.Parameters.Add(new(reference));
            }
        }
        return fptrType;
    }
}
