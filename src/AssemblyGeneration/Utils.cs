using Hosihikari.Generation.Generator;
using Hosihikari.Generation.Parser;
using Hosihikari.Generation.Utils;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using static Hosihikari.Generation.Utils.OriginalData.Class;
using static Hosihikari.Generation.AssemblyGeneration.AssemblyBuilder;

namespace Hosihikari.Generation.AssemblyGeneration;

public static class Utils
{
    [Flags]
    public enum PropertyMethodType
    {
        Get,
        Set
    }

    static Utils()
    {
        FileStream file = File.OpenRead("System.Runtime.dll");
        AssemblyDefinition? asm = AssemblyDefinition.ReadAssembly(file);
        file.Close();

        foreach (TypeDefinition? type in asm.Modules.SelectMany(module => module.Types))
        {
            switch (type.Name)
            {
                case "Object":
                    Object = type;
                    break;
                case "String":
                    String = type;
                    break;
                case nameof(System.IDisposable):
                    IDisposable = type;
                    break;
                case "GC":
                    GC = type;
                    break;
                case "ValueType":
                    ValueType = type;
                    break;
            }
        }
    }

    public static TypeDefinition? Object { get; private set; }
    public static TypeDefinition? String { get; private set; }
    public static TypeDefinition? IDisposable { get; private set; }
    public static TypeDefinition? GC { get; private set; }
    public static TypeDefinition? ValueType { get; private set; }

    public static string SelectOperatorName(in Item t)
    {
        //https://learn.microsoft.com/en-us/cpp/cpp/operator-overloading?view=msvc-170
        if (t.Params is null)
        {
            throw new InvalidDataException();
        }

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

    public static string GetParameterName(in Item item, bool hasThis, int paramIndex)
    {
        if (item.ParamsName is not null && (item.ParamsName.Count > paramIndex))
        {
            return item.ParamsName[paramIndex];
        }

        return $"a{paramIndex - (hasThis ? 1 : 0)}";
    }

    public static bool HasThis(ItemAccessType accessType)
    {
        return accessType is ItemAccessType.Public or ItemAccessType.Protected or ItemAccessType.Private
            or ItemAccessType.Virtual or ItemAccessType.VirtualUnordered;
    }

    public static string BuildFptrId(in Item t)
    {
        StringBuilder builder = new();
        string prefix = (SymbolType)t.SymbolType switch
        {
            SymbolType.Constructor => "ctor_",
            SymbolType.Destructor => "dtor_",
            _ => string.Empty
        };

        builder.Append(prefix);
        foreach (char c in t.Symbol)
        {
            builder.Append(TypeAnalyzer.IsLetterOrUnderline(c) ? c : '_');
        }

        return builder.ToString();
    }

    public static string BuildFptrName(HashSet<string> fptrFieldNames, in Item t)
    {
        string fptrName = t.Name;

        if (fptrName.Contains("operator"))
        {
            fptrName = $"cpp_{SelectOperatorName(t)}";
        }

        if ((SymbolType)t.SymbolType is SymbolType.Destructor)
        {
            fptrName = $"destructor_{fptrName[1..]}";
        }

        if ((SymbolType)t.SymbolType is SymbolType.Constructor)
        {
            fptrName = $"constructor_{fptrName}";
        }


        if (!fptrFieldNames.Contains(fptrName))
        {
            return fptrName;
        }

        StringBuilder builder = new(fptrName);
        if (t.Params is not null)
        {
            foreach (Item.TypeData param in t.Params)
            {
                builder.Append('_');
                foreach (char c in param.Name)
                {
                    builder.Append(TypeAnalyzer.IsLetterOrUnderline(c) ? c : '_');
                }
            }
        }

        fptrName = builder.ToString();

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
        FunctionPointerType fptrType = new() { CallingConvention = MethodCallingConvention.Unmanaged };
        TypeData typeData = (SymbolType)t.SymbolType switch
        {
            SymbolType.Constructor or SymbolType.Destructor => new(new() { Name = "void" }),
            _ => new(t.Type)
        };

        TypeReference @ref = TypeReferenceBuilder.BuildReference(definedTypes, module, typeData, true);
        fptrType.ReturnType = @ref;

        if (HasThis(itemAccessType))
        {
            if (isExtension)
            {
                if (extensionType is not null && extensionType.IsValueType)
                {
                    ParameterDefinition param = new(
                        string.Empty,
                        ParameterAttributes.None,
                        module.ImportReference(extensionType).MakeByReferenceType());

                    fptrType.Parameters.Add(param);
                }
                else
                {
                    fptrType.Parameters.Add(new(module.ImportReference(typeof(nint))));
                }
            }
            else
            {
                fptrType.Parameters.Add(new(module.ImportReference(typeof(nint))));
            }
        }

        if (t.Params is null)
        {
            return (fptrType, isVarArg);
        }

        for (int i = 0; i < t.Params.Count; i++)
        {
            TypeData type = new(t.Params[i]);
            if (type.Analyzer.CppTypeHandle.Type is CppTypeEnum.VarArgs && (i == (t.Params.Count - 1)))
            {
                fptrType.Parameters.Add(new("args", ParameterAttributes.None,
                    module.ImportReference(typeof(RuntimeArgumentHandle))));
                isVarArg = true;
                continue;
            }

            TypeReference reference = TypeReferenceBuilder.BuildReference(definedTypes, module, type);
            fptrType.Parameters.Add(new(reference));
        }

        return (fptrType, isVarArg);
    }


    public static bool IsPropertyMethod(MethodDefinition method,
        [NotNullWhen(true)] out (PropertyMethodType propertyMethodType, string proeprtyName)? tuple)
    {
        tuple = null;

        if (method.Name.Length > 3)
        {
            string str = method.Name[..3].ToLower();
            switch (str)
            {
                case "get" when method.Parameters.Count is 0:
                    tuple = (PropertyMethodType.Get, method.Name[3..]);
                    return true;
                case "set" when method.Parameters.Count is 1:
                    tuple = (PropertyMethodType.Set, method.Name[3..]);
                    return true;
            }
        }
        else if (method.Name.StartsWith("Is") || method.Name.StartsWith("is"))
        {
            tuple = (PropertyMethodType.Get, method.Name);
            return true;
        }
        else if (method.Name.StartsWith("Has") || method.Name.StartsWith("has"))
        {
            tuple = (PropertyMethodType.Get, method.Name);
            return true;
        }

        return false;
    }
}