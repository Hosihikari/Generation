using System.Text;
using System.Diagnostics.CodeAnalysis;

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
            "operator()" => t.Params is null ? "operator_Function_call" : t.Params.Count is 1 ? "operator_Cast" : "operator_Function_call",
            "operator*" => t.Params is null ? "operator_Pointer_dereference" : t.Params.Count is 1 ? "operator_Pointer_dereference" : "operator_Multiplication",
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

        if (fptrName.Contains("operator"))
            fptrName = $"cpp_{SelectOperatorName(t)}";

        if ((SymbolType)t.SymbolType is SymbolType.Destructor)
            fptrName = $"destructor_{fptrName[1..]}";
        if ((SymbolType)t.SymbolType is SymbolType.Constructor)
            fptrName = $"constructor_{fptrName}";


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
            fptrName = builder.ToString();
        }

        return fptrName;
    }

    public static (FunctionPointerType fptrType, bool isVarArg) BuildFunctionPointerType(
        ModuleDefinition module,
        Dictionary<string, TypeDefinition> definedTypes,
        ItemAccessType itemAccessType,
        in Item t,
        bool isExtension = false,
        Type? extensionType = null)
    {
        bool isVarArg = false;
        var fptrType = new FunctionPointerType { CallingConvention = MethodCallingConvention.Unmanaged };
        var typeData = (SymbolType)t.SymbolType switch
        {
            SymbolType.Constructor or SymbolType.Destructor => new TypeData(new() { Name = "void" }),
            _ => new TypeData(t.Type)
        };

        var (@ref, _) = TypeReferenceBuilder.BuildReference(definedTypes, module, typeData, true);
        fptrType.ReturnType = @ref;

        if (HasThis(itemAccessType))
        {
            if (isExtension)
            {
                if (extensionType is not null && extensionType.IsValueType)
                {
                    var param = new ParameterDefinition(
                        string.Empty,
                        ParameterAttributes.None,
                        module.ImportReference(extensionType).MakeByReferenceType());

                    fptrType.Parameters.Add(param);
                }
                else fptrType.Parameters.Add(new(module.TypeSystem.IntPtr));
            }
            else fptrType.Parameters.Add(new(module.TypeSystem.IntPtr));
        }

        if (t.Params is null)
            return (fptrType, false);
        else
        {
            for (int i = 0; i < t.Params.Count; i++)
            {
                var type = new TypeData(t.Params[i]);
                if (type.Analyzer.CppTypeHandle.Type is CppTypeEnum.VarArgs && i == t.Params.Count - 1)
                {
                    fptrType.Parameters.Add(new("args", ParameterAttributes.None, module.ImportReference(typeof(RuntimeArgumentHandle))));
                    isVarArg = true;
                    continue;
                }

                var (reference, _) = TypeReferenceBuilder.BuildReference(definedTypes, module, type);
                fptrType.Parameters.Add(new(reference));
            }
        }
        return (fptrType, isVarArg);
    }

    public static bool IsVarArg(IMethodSignature self) => (self.CallingConvention & MethodCallingConvention.VarArg) != 0;

    [Flags]
    public enum PropertyMethodType { Get, Set }



    public static bool IsPropertyMethod(MethodDefinition method, [NotNullWhen(true)] out (PropertyMethodType propertyMethodType, string proeprtyName)? tuple)
    {
        tuple = null;

        if (method.Name.Length > 3)
        {
            var str = method.Name[..3].ToLower();
            if (str is "get" && method.Parameters.Count is 0)
            {
                tuple = (PropertyMethodType.Get, method.Name[3..]);
                return true;
            }
            else if (str is "set" && method.Parameters.Count is 1)
            {
                tuple = (PropertyMethodType.Set, method.Name[3..]);
                return true;
            }
        }
        else if (method.Name.StartsWith("Is"))
        {
            tuple = (PropertyMethodType.Get, method.Name);
            return true;
        }

        return false;
    }
}
