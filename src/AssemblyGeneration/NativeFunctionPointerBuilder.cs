using Hosihikari.NativeInterop;
using Hosihikari.NativeInterop.Unmanaged.Attributes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using static Hosihikari.Generation.Utils.OriginalData.Class;
using static Hosihikari.Generation.AssemblyGeneration.AssemblyBuilder;

namespace Hosihikari.Generation.AssemblyGeneration;

public class NativeFunctionPointerBuilder(ModuleDefinition module)
{
    public static TypeDefinition BuildOriginalType()
    {
        return new(string.Empty, "Original",
            TypeAttributes.Interface |
            TypeAttributes.NestedPublic |
            TypeAttributes.AutoClass |
            TypeAttributes.AnsiClass |
            TypeAttributes.Abstract);
    }


    public TypeDefinition BuildFptrStorageType(string fptrId, in Item t, out FieldDefinition fptrField)
    {
        TypeDefinition fptrStorageType = new(string.Empty, $"__FptrStorageType_{fptrId}",
            TypeAttributes.NestedPrivate | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.Class,
            module.ImportReference(Utils.Object));

        FieldDefinition _fptrField = new(
            "funcPointer", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly,
            module.ImportReference(typeof(void).MakePointerType()));

        fptrStorageType.Fields.Add(_fptrField);

        MethodDefinition cctor = new(".cctor",
            MethodAttributes.Static | MethodAttributes.Private | MethodAttributes.HideBySig |
            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.ImportReference(typeof(void)));

        fptrStorageType.Methods.Add(cctor);
        {
            ILProcessor? il = cctor.Body.GetILProcessor();
            il.Emit(OC.Ldstr, t.Symbol);
            il.Emit(OC.Call, module.ImportReference(typeof(SymbolHelper).GetMethod(nameof(SymbolHelper.DlsymPointer))));
            il.Emit(OC.Stsfld, _fptrField);
            il.Emit(OC.Ret);
        }

        fptrField = _fptrField;
        return fptrStorageType;
    }

    public (PropertyDefinition property, MethodDefinition getMethod, FunctionPointerType fptrType, MethodDefinition
        staticMethod) BuildFptrProperty(
            ItemAccessType itemAccessType,
            Dictionary<string, TypeDefinition> definedTypes,
            HashSet<string> fptrFieldNames,
            in Item t,
            FieldDefinition fptrField,
            bool isExtension = false,
            Type? extensionType = null)
    {
        string fptrName = Utils.BuildFptrName(fptrFieldNames, t);
        fptrFieldNames.Add(fptrName);

        (FunctionPointerType fptrType, _) =
            Utils.BuildFunctionPointerType(module, definedTypes, itemAccessType, t, isExtension, extensionType);


        PropertyDefinition fptrPropertyDef = new(
            $"FunctionPointer_{fptrName}",
            PropertyAttributes.None,
            fptrType);

        MethodDefinition getMethodDef = new($"get_FunctionPointer_{fptrName}",
            MethodAttributes.Public |
            MethodAttributes.SpecialName |
            MethodAttributes.HideBySig |
            MethodAttributes.Static,
            fptrType);

        {
            ILProcessor? il = getMethodDef.Body.GetILProcessor();
            il.Emit(OC.Ldsfld, fptrField);
            il.Emit(OC.Ret);
        }

        fptrPropertyDef.GetMethod = getMethodDef;

        string methodName = fptrName.Length > 1 ? $"{char.ToUpper(fptrName[0])}{fptrName[1..]}" : fptrName.ToUpper();

        MethodDefinition method = new(methodName, MethodAttributes.Public | MethodAttributes.Static,
            fptrType.ReturnType);
        CallSite callSite = new(module.ImportReference(fptrType.ReturnType));
        for (int i = 0; i < fptrType.Parameters.Count; i++)
        {
            callSite.Parameters.Add(fptrType.Parameters[i]);
            method.Parameters.Add(new($"a{i}", fptrType.Parameters[i].Attributes,
                fptrType.Parameters[i].ParameterType));
        }

        {
            ILProcessor? il = method.Body.GetILProcessor();

            foreach (ParameterDefinition? param in method.Parameters)
            {
                il.Emit(OC.Ldarg, param);
            }

            il.Emit(OC.Ldsfld, fptrField);

            il.Emit(OC.Calli, callSite);
            il.Emit(OC.Ret);
        }

        CustomAttribute attr = new(module.ImportReference(typeof(SymbolAttribute).GetConstructors().First()));
        attr.ConstructorArguments.Add(new(Utils.String, t.Symbol));
        fptrPropertyDef.CustomAttributes.Add(attr);
        method.CustomAttributes.Add(attr);

        attr = new(module.ImportReference(typeof(RVAAttribute).GetConstructors().First()));
        attr.ConstructorArguments.Add(new(module.ImportReference(typeof(ulong)), t.RVA));
        fptrPropertyDef.CustomAttributes.Add(attr);
        method.CustomAttributes.Add(attr);

        return (fptrPropertyDef, getMethodDef, fptrType, method);
    }
}