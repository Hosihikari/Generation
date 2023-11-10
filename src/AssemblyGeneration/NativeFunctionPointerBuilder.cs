using System.Diagnostics.CodeAnalysis;

using Hosihikari.NativeInterop;

using Mono.Cecil;

using static Hosihikari.Utils.OriginalData.Class;
using static Hosihikari.Generation.AssemblyGeneration.AssemblyBuilder;

namespace Hosihikari.Generation.AssemblyGeneration;

public class NativeFunctionPointerBuilder
{
    public ModuleDefinition module;

    public Random random;


    public NativeFunctionPointerBuilder(ModuleDefinition module)
    {
        this.module = module;
        random = new();
    }

    public static TypeDefinition BuildOriginalType(TypeDefinition definition, bool isExtension = false) =>
         new(string.Empty, $"I{definition.Name}Original", TypeAttributes.NestedPublic | TypeAttributes.Interface);


    public TypeDefinition BuildFptrStorageType(string fptrId, in Item t, out FieldDefinition fptrField)
    {
        var fptrStorageType = new TypeDefinition(string.Empty, $"__FptrStorageType_{fptrId}",
                    TypeAttributes.NestedPrivate | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Class);

        var _fptrField = new FieldDefinition(
            $"__Field_{fptrId}", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly, module.ImportReference(typeof(void).MakePointerType()));

        fptrStorageType.Fields.Add(_fptrField);

        var cctor = new MethodDefinition(".cctor",
            MethodAttributes.Static | MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.ImportReference(typeof(void)));

        fptrStorageType.Methods.Add(cctor);
        {
            var il = cctor.Body.GetILProcessor();
            il.Emit(OC.Ldstr, t.Symbol);
            il.Emit(OC.Call, module.ImportReference(typeof(SymbolHelper).GetMethod(nameof(SymbolHelper.DlsymPointer))));
            il.Emit(OC.Stsfld, _fptrField);
            il.Emit(OC.Ret);
        }

        fptrField = _fptrField;
        return fptrStorageType;
    }

    public (PropertyDefinition property, MethodDefinition getMethod, FunctionPointerType fptrType) BuildFptrProperty(
        ItemAccessType itemAccessType,
        Dictionary<string, TypeDefinition> definedTypes,
        HashSet<string> fptrFieldNames,
        in Item t,
        FieldDefinition fptrField,
        bool isExtension = false,
        Type? extensionType = null)
    {

        var fptrName = Utils.BuildFptrName(fptrFieldNames, t, random);
        fptrFieldNames.Add(fptrName);

        var (fptrType, _) = Utils.BuildFunctionPointerType(module, definedTypes, itemAccessType, t, isExtension, extensionType);


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
            il.Emit(OC.Ldsfld, fptrField);
            il.Emit(OC.Ret);
        }

        fptrPropertyDef.GetMethod = getMethodDef;

        return (fptrPropertyDef, getMethodDef, fptrType);
    }
}
