using Hosihikari.Generation.Generator;
using Hosihikari.NativeInterop;
using Hosihikari.NativeInterop.Unmanaged;
using Hosihikari.Utils;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using System;
using System.Text;
using static Hosihikari.Utils.OriginalData.Class;

namespace Hosihikari.Generation.AssemblyGeneration;

public partial class AssemblyBuilder
{
    private readonly HashSet<string> fptrFieldNames = new();

    private static string BuildFptrId(in Item t)
    {
        StringBuilder builder = new();
        foreach (var c in t.Symbol)
            builder.Append(TypeAnalyzer.IsLetterOrUnderline(c) ? c : '_');
        return builder.ToString();
    }

    private TypeDefinition BuildFptrStorageType(string fptrId, in Item t, out FieldDefinition fptrField)
    {
        var fptrStorageType = new TypeDefinition(string.Empty, $"__FptrStorageType_{fptrId}",
                    TypeAttributes.NestedPrivate | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Class);

        var fptrFieldType = new FunctionPointerType();
        {
            fptrFieldType.CallingConvention = MethodCallingConvention.Unmanaged;
            var retType = new TypeData(t.Type);
        }

        var _fptrField = new FieldDefinition(
            $"__Field_{fptrId}", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly, module.ImportReference(typeof(void).MakePointerType()));

        fptrStorageType.Fields.Add(_fptrField);

        var cctor = new MethodDefinition(".cctor",
            MethodAttributes.Static | MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.TypeSystem.Void);

        fptrStorageType.Methods.Add(cctor);
        {
            var il = cctor.Body.GetILProcessor();
            il.Append(il.Create(oc.Ldstr, t.Symbol));
            il.Append(il.Create(oc.Call, module.ImportReference(typeof(SymbolHelper).GetMethod(nameof(SymbolHelper.DlsymPointer)))));
            il.Append(il.Create(oc.Stsfld, _fptrField));
            il.Append(il.Create(oc.Ret));
        }

        fptrField = _fptrField;
        return fptrStorageType;
    }

    private static bool HasThis(ItemAccessType accessType) =>
        accessType is ItemAccessType.Public ||
        accessType is ItemAccessType.Protected ||
        accessType is ItemAccessType.Virtual;

    //private string SelectCppOperatorName(string op)
    //{
    //    //https://learn.microsoft.com/en-us/cpp/cpp/operator-overloading?view=msvc-170
    //    return op switch
    //    {
    //        "operator," => "cppOperator_Comma",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!=" => "cppOperator_Inequality",
    //        "operator%" => "cppOperator_Modulus",
    //        "operator%=" => "cppOperator_Modulus_assignment",
    //        "operator&" => "cppOperator_Bitwise_AND",
    //        "operator&&" => "cppOperator_Logical_AND",
    //        "operator&=" => "cppOperator_Bitwise_AND_assignment",
    //        "operator()" => "cppOperator_Function_call",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //        "operator!" => "cppOperator_Logical_NOT",
    //    };
    //}

    private string BuildFptrName(in Item t, Random random)
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

        static ulong Hash(string str)
        {
            ulong rval = 0;
            for (int i = 0; i < str.Length; ++i)
            {
                if ((i & 1) > 0)
                {
                    rval ^= (~((rval << 11) ^ str[i] ^ (rval >> 5)));
                }
                else
                {
                    rval ^= (~((rval << 7) ^ str[i] ^ (rval >> 3)));
                }
            }
            return rval;
        }

        if (fptrName.Contains("operator"))
            fptrName = $"cpp_operator_{Hash(fptrName)}";

        return fptrName;
    }

    private (TypeReference @ref, string name) BuildReference(in TypeData type, bool isResult = false)
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

    private FunctionPointerType BuildFunctionPointerType(ItemAccessType itemAccessType, TypeDefinition definition, in Item t)
    {
        var fptrType = new FunctionPointerType { CallingConvention = MethodCallingConvention.Unmanaged };
        var typeData = new TypeData(t.Type);
        var (@ref, _) = BuildReference(typeData);
        fptrType.ReturnType = @ref;

        if (HasThis(itemAccessType))
            fptrType.Parameters.Add(new(module.TypeSystem.IntPtr));

        if (t.Params is null)
            return fptrType;
        else
        {
            foreach (var param in t.Params)
            {
                var (reference, _) = BuildReference(new(param));
                fptrType.Parameters.Add(new(reference));
            }
        }
        return fptrType;
    }

    private void BuildFptrProperty(ItemAccessType itemAccessType, TypeDefinition definition, TypeDefinition originalTypeDefinition, Random random, in Item t, FieldDefinition fptrField)
    {
        var fptrName = BuildFptrName(t, random);
        fptrFieldNames.Add(fptrName);

        var fptrType = BuildFunctionPointerType(itemAccessType, definition, t);


        var fptrPropertyDef = new PropertyDefinition(
            $"FunctionPointer_{fptrName}",
            PropertyAttributes.None,
            fptrType);

        var getMethodDef = new MethodDefinition($"get_FunctionPointer_{fptrName}",
            MethodAttributes.Public |
            MethodAttributes.SpecialName |
            MethodAttributes.HideBySig |
            MethodAttributes.Static,
            fptrType);

        {
            var il = getMethodDef.Body.GetILProcessor();
            il.Append(il.Create(oc.Ldsfld, fptrField));
            il.Append(il.Create(oc.Ret));
        }

        originalTypeDefinition.Properties.Add(fptrPropertyDef);
        originalTypeDefinition.Methods.Add(getMethodDef);

        fptrPropertyDef.GetMethod = getMethodDef;
    }

    private void BuildFunctionPointer()
    {

        while (items.TryDequeue(out var item))
        {
            var type = item.type;
            var list = item.list;

            var originalType = type.NestedTypes.First(t => t.Name == $"I{type.Name}Original");

            foreach (var (accessType, t) in list)
            {
                if ((SymbolType)t.SymbolType is SymbolType.StaticField)
                    continue;

                try
                {
                    string fptrId = BuildFptrId(t);
                    var fptrStorageType = BuildFptrStorageType(fptrId, t, out var fptrField);
                    originalType.NestedTypes.Add(fptrStorageType);

                    Random random = new();
                    BuildFptrProperty(accessType, type, originalType, random, t, fptrField);
                }
                catch (Exception) { continue; }
            }


        }

    }
}
