using Hosihikari.Generation.CppParser;
using Hosihikari.Generation.Utils;
using Hosihikari.NativeInterop.Unmanaged;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Hosihikari.Generation.ILGenerate;

public class TypeGenerator
{
    public const string OriginalTypeName = "Original";
    public const string FunctionPointerDefintionTypeName = "FunctionPointerDefintion";
    public const string PointerFieldName = "__pointer_";
    public const string OwnsInstanceFieldName = "__isOwner_";
    public const string OwnsMemoryFieldName = "__isTempStackValue_";
    public const string PointerPropertyName = "Pointer";
    public const string OwnsInstancePropertyName = "IsOwner";
    public const string OwnsMemoryPropertyName = "IsTempStackValue";

    public const string DefaultNamespace = "Hosihikari.Minecraft";

    public AssemblyGenerator Assembly { get; }

    public OriginalClass? Class { get; private set; }

    public CppType ParsedType { get; }

    public List<MethodGenerator> Methods { get; } = [];
    public List<StaticFieldGenerator> StaticFields { get; } = [];

    public TypeDefinition Type { get; }

    public TypeDefinition OriginalType { get; }

    public TypeDefinition FunctionPointerDefintionType { get; }

    public bool IsEmpty => Class is null;

    public bool Generated { get; private set; }

    public bool DestructorGenerated { get; private set; }

    public PropertyDefinition? PointerProperty { get; private set; }
    public PropertyDefinition? IsOwnerProperty { get; private set; }
    public PropertyDefinition? OwnsMemoryProperty { get; private set; }

    private TypeGenerator(AssemblyGenerator assemblyGenerator, OriginalClass @class, CppType cppType)
    {
        Assembly = assemblyGenerator;

        Class = @class;
        ParsedType = cppType;

        Type = Assembly.MainModule.DefineType(
            GenerateNamespace(cppType.RootType.Namespaces),
            cppType.RootType.TypeIdentifier,
            TypeAttributes.Class |
            TypeAttributes.Public);
        Type.Interfaces.Add(
            new(
                new GenericInstanceType(Assembly.ImportRef(typeof(ICppInstance<>)))
                {
                    GenericArguments = { Type }
                }));
        Type.Interfaces.Add(new(Assembly.ImportRef(typeof(ICppInstanceNonGeneric))));

        OriginalType = Type.DefineType(
            string.Empty,
            OriginalTypeName,
            TypeAttributes.Interface |
            TypeAttributes.NestedPublic |
            TypeAttributes.AutoClass |
            TypeAttributes.AnsiClass |
            TypeAttributes.Abstract);

        FunctionPointerDefintionType = OriginalType.DefineType(
            string.Empty,
            FunctionPointerDefintionTypeName,
            TypeAttributes.NestedPublic |
            TypeAttributes.Sealed |
            TypeAttributes.Abstract |
            TypeAttributes.Class);
    }

    private TypeGenerator(AssemblyGenerator assemblyGenerator, CppType cppType)
    {
        Assembly = assemblyGenerator;

        ParsedType = cppType;

        Type = Assembly.Module.DefineType(
            GenerateNamespace(cppType.RootType.Namespaces),
            cppType.RootType.TypeIdentifier,
            TypeAttributes.Class |
            TypeAttributes.Public);
        Type.Interfaces.Add(
            new(
                new GenericInstanceType(Assembly.ImportRef(typeof(ICppInstance<>)))
                {
                    GenericArguments = { Type }
                }));
        Type.Interfaces.Add(new(Assembly.ImportRef(typeof(ICppInstanceNonGeneric))));

        OriginalType = Type.DefineType(
            string.Empty,
            OriginalTypeName,
            TypeAttributes.Interface |
            TypeAttributes.NestedPublic |
            TypeAttributes.AutoClass |
            TypeAttributes.AnsiClass |
            TypeAttributes.Abstract);

        FunctionPointerDefintionType = OriginalType.DefineType(
            string.Empty,
            FunctionPointerDefintionTypeName,
            TypeAttributes.NestedPublic |
            TypeAttributes.Sealed |
            TypeAttributes.Abstract |
            TypeAttributes.Class);
    }

    public static bool TryCreateTypeGenerator(AssemblyGenerator assemblyGenerator, string type, OriginalClass @class, [NotNullWhen(true)] out TypeGenerator? typeGenerator)
    {
        if (CppTypeParser.TryParse(type, out var cppType))
        {
            typeGenerator = new(assemblyGenerator, @class, cppType.RootType);
            return true;
        }
        else
        {
            typeGenerator = null;
            return false;
        }
    }

    public static bool TryCreateEmptyTypeGenerator(AssemblyGenerator assemblyGenerator, string type, [NotNullWhen(true)] out TypeGenerator? typeGenerator)
    {
        if (CppTypeParser.TryParse(type, out var cppType))
        {
            typeGenerator = new(assemblyGenerator, cppType.RootType);
            return true;
        }
        else
        {
            typeGenerator = null;
            return false;
        }
    }

    public static bool TryCreateTypeGenerator(AssemblyGenerator assemblyGenerator, CppType type, OriginalClass @class, [NotNullWhen(true)] out TypeGenerator? typeGenerator)
    {
        typeGenerator = new(assemblyGenerator, @class, type.RootType);
        return true;
    }

    public static bool TryCreateEmptyTypeGenerator(AssemblyGenerator assemblyGenerator, CppType type, [NotNullWhen(true)] out TypeGenerator? typeGenerator)
    {
        typeGenerator = new(assemblyGenerator, type.RootType);
        return true;
    }

    private static string GenerateNamespace(string[]? namespaces)
    {
        if (namespaces is null)
            return DefaultNamespace;

        StringBuilder builder = new(DefaultNamespace);

        foreach (var temp in namespaces)
        {
            builder.Append("._");
            foreach (var c in temp)
            {
                if (char.IsLetter(c) || char.IsDigit(c))
                    builder.Append(c);
                else
                    builder.Append('_');
            }
        }

        return builder.ToString();
    }

    public async ValueTask<bool> GenerateAsync()
    {
        if (Generated)
            return true;

        Generated = true;

        await GenerateImplAsync();

        if (Class is null)
            return false;

        var itemsAndAccessType = Class.GetAllItemsWithAccessType();
        foreach (var (accessType, isStatic, items) in itemsAndAccessType)
        {
            if (items is null)
                continue;

            foreach (var item in items)
            {
                if ((SymbolType)item.SymbolType == SymbolType.StaticField)
                {
                    //StaticFieldGenerator.TryCreateStaticFieldGenerator(item, this, out var staticFieldGenerator);
                }
                else
                {
                    if (MethodGenerator.TryCreateMethodGenerator(accessType, isStatic, item, this, out var methodGenerator))
                    {
                        await methodGenerator.GenerateAsync();
                        Methods.Add(methodGenerator);
                    }
                }
            }
        }

        if (DestructorGenerated is false)
            await GenerateDtorDefinitionAsync(il => il.Emit(OC.Ret));

        return true;
    }

    public void SetOriginalClass(OriginalClass @class)
    {
        if (IsEmpty is false)
            throw new InvalidOperationException("TypeGenerator is already initialized.");

        Class = @class;
        Generated = false;
    }

    [MemberNotNull(nameof(PointerProperty), nameof(IsOwnerProperty), nameof(OwnsMemoryProperty))]
    private async ValueTask GenerateImplAsync()
#pragma warning disable CS8774
        => await Task.Run(() =>
        {
            var (_, ptrProperty) = GenerateProperty(PointerFieldName, PointerPropertyName, Assembly.ImportRef(typeof(nint)));
            var (_, ownsInstanceProperty) = GenerateProperty(OwnsInstanceFieldName, OwnsInstancePropertyName, Assembly.ImportRef(typeof(bool)));
            var (_, ownsMemoryProperty) = GenerateProperty(OwnsMemoryFieldName, OwnsMemoryPropertyName, Assembly.ImportRef(typeof(bool)));

            PointerProperty = ptrProperty;
            IsOwnerProperty = ownsInstanceProperty;
            OwnsMemoryProperty = ownsMemoryProperty;

            GenerateDefaultCtor(ptrProperty, ownsInstanceProperty, ownsMemoryProperty);
            BuildClassSizeProperty(0);
            BuildImplicitOperator();
        });
#pragma warning restore CS8774

    private (FieldDefinition field, PropertyDefinition property) GenerateProperty(
        string fieldName,
        string propertyName,
        TypeReference type)
    {
        ILProcessor il;

        var field = Type.DefineField(
            fieldName,
            FieldAttributes.Private,
            type);

        var property = Type.DefineProperty(
            propertyName,
             PropertyAttributes.None,
             type);
        MethodDefinition getMethod = Type.DefineMethod(
            $"get_{propertyName}",
            MethodAttributes.Public |
            MethodAttributes.Final |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.NewSlot |
            MethodAttributes.Virtual,
            type);
        il = getMethod.Body.GetILProcessor();
        il.Emit(OC.Ldarg_0);
        il.Emit(OC.Ldfld, field);
        il.Emit(OC.Ret);

        MethodDefinition setMethod = Type.DefineMethod(
           $"set_{propertyName}",
           MethodAttributes.Public |
           MethodAttributes.Final |
           MethodAttributes.HideBySig |
           MethodAttributes.SpecialName |
           MethodAttributes.NewSlot |
           MethodAttributes.Virtual,
           Assembly.ImportRef(typeof(void)),
           parameterTypes: [new ParameterDefinition("value", ParameterAttributes.None, type)]);
        il = setMethod.Body.GetILProcessor();
        il.Emit(OC.Ldarg_0);
        il.Emit(OC.Ldarg_1);
        il.Emit(OC.Stfld, field);
        il.Emit(OC.Ret);

        property.BindMethods(getMethod, setMethod);

        return (field, property);
    }

    private void GenerateDefaultCtor(
        PropertyDefinition ptr,
        PropertyDefinition owns,
        PropertyDefinition ownsMemory)
    {
        MethodDefinition ctorDefault = Type.DefineMethod(
            ".ctor",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName,
            Assembly.ImportRef(typeof(void)));
        ILProcessor? il = ctorDefault.Body.GetILProcessor();
        il.Emit(OC.Ldarg_0);
        il.Emit(OC.Call, Assembly.ImportRef(Assembly.TypeSystem.Object.GetConstructors().First()));
        il.Emit(OC.Ldarg_0);
        il.Emit(OC.Ldc_I8, 0L);
        il.Emit(OC.Conv_Ovf_I);
        il.Emit(OC.Call, ptr.SetMethod);
        il.Emit(OC.Ldarg_0);
        il.Emit(OC.Ldc_I4_0);
        il.Emit(OC.Call, owns.SetMethod);
        il.Emit(OC.Ldarg_0);
        il.Emit(OC.Ldc_I4_0);
        il.Emit(OC.Call, ownsMemory.SetMethod);
        il.Emit(OC.Ret);

        MethodDefinition ctor = Type.DefineMethod(
            ".ctor",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName,
            Assembly.ImportRef(typeof(void)),
            parameterTypes: [
                new("ptr", ParameterAttributes.None, Assembly.ImportRef(typeof(nint))),
                new("owns", ParameterAttributes.Optional, Assembly.ImportRef(typeof(bool)))
                {
                    Constant = false
                },
                new("isTempStackValue", ParameterAttributes.Optional, Assembly.ImportRef(typeof(bool)))
                {
                    Constant = true
                }]);
        il = ctor.Body.GetILProcessor();
        il.Emit(OC.Ldarg_0);
        il.Emit(OC.Call, Assembly.ImportRef(Assembly.TypeSystem.Object.GetConstructors().First()));
        il.Emit(OC.Ldarg_0);
        il.Emit(OC.Ldarg_1);
        il.Emit(OC.Call, ptr.SetMethod);
        il.Emit(OC.Ldarg_0);
        il.Emit(OC.Ldarg_2);
        il.Emit(OC.Call, owns.SetMethod);
        il.Emit(OC.Ldarg_0);
        il.Emit(OC.Ldarg_3);
        il.Emit(OC.Call, ownsMemory.SetMethod);
        il.Emit(OC.Ret);

        GenericInstanceType IcppInstanceType = new(Assembly.ImportRef(typeof(ICppInstance<>)))
        {
            GenericArguments = { Type }
        };

        MethodReference methodReference = new("ConstructInstance",
            IcppInstanceType.Resolve().GenericParameters[0], IcppInstanceType)
        {
            Parameters =
            {
                new(Assembly.ImportRef(typeof(nint))),
                new(Assembly.ImportRef(typeof(bool))),
                new(Assembly.ImportRef(typeof(bool)))
            }
        };

        MethodDefinition ConstructInstanceMethod = Type.DefineMethod(
            "ConstructInstance",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.Static,
            Type,
            parameterTypes: [
                new("ptr", ParameterAttributes.None, Assembly.ImportRef(typeof(nint))),
                new("owns", ParameterAttributes.Optional, Assembly.ImportRef(typeof(bool)))
                {
                    Constant = false
                },
                new("isTempStackValue", ParameterAttributes.Optional, Assembly.ImportRef(typeof(bool)))
                {
                    Constant = true
                }]);
        ConstructInstanceMethod.Overrides.Add(Assembly.ImportRef(methodReference));
        il = ConstructInstanceMethod.Body.GetILProcessor();
        il.Emit(OC.Ldarg_0);
        il.Emit(OC.Ldarg_1);
        il.Emit(OC.Ldarg_2);
        il.Emit(OC.Newobj, ctor);
        il.Emit(OC.Ret);

        MethodDefinition ConstructInstanceNonGenericMethod = Type.DefineMethod(
            $"{typeof(ICppInstanceNonGeneric).FullName}.ConstructInstance",
            MethodAttributes.Private |
            MethodAttributes.HideBySig |
            MethodAttributes.Static,
            Assembly.ImportRef(typeof(object)),
            parameterTypes: [
                new("ptr", ParameterAttributes.None, Assembly.ImportRef((typeof(nint)))),
                new("owns", ParameterAttributes.None, Assembly.ImportRef((typeof(bool)))),
                new("isTempStackValue", ParameterAttributes.None, Assembly.ImportRef(typeof(bool)))
                ]);
        ConstructInstanceNonGenericMethod.Overrides.Add(
            Assembly.ImportRef(
                typeof(ICppInstanceNonGeneric)
                .GetMethods()
                .First(f => f.Name == nameof(ICppInstanceNonGeneric.ConstructInstance))));
        il = ConstructInstanceNonGenericMethod.Body.GetILProcessor();
        il.Emit(OC.Ldarg_0);
        il.Emit(OC.Ldarg_1);
        il.Emit(OC.Ldarg_2);
        il.Emit(OC.Call, ConstructInstanceMethod);
        il.Emit(OC.Ret);
    }

    private void BuildClassSizeProperty(ulong classSize)
    {
        PropertyDefinition property = Type.DefineProperty(
            "ClassSize", PropertyAttributes.None, Assembly.ImportRef(typeof(ulong)));

        MethodDefinition getMethod = Type.DefineMethod(
            "get_ClassSize",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.Static,
            Assembly.ImportRef(typeof(ulong)));
        getMethod.Overrides.Add(Assembly.ImportRef(
            typeof(ICppInstanceNonGeneric).GetMethods().First(f => f.Name is "get_ClassSize")));
        ILProcessor? il = getMethod.Body.GetILProcessor();
        il.Emit(OC.Ldc_I8, (long)classSize);
        il.Emit(OC.Conv_U8);
        il.Emit(OC.Ret);

        property.BindMethods(getMethod: getMethod);
    }

    private void BuildImplicitOperator()
    {
        GenericInstanceType IcppInstanceType = new(Assembly.ImportRef(typeof(ICppInstance<>)))
        {
            GenericArguments = { Type }
        };

        MethodDefinition op_2_nint = Type.DefineMethod(
            "op_Implicit",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.Static,
            Assembly.ImportRef(typeof(nint)),
            parameterTypes: [new("ins", ParameterAttributes.None, Type)]);

        op_2_nint.Overrides.Add(
            new("op_Implicit", Assembly.ImportRef(typeof(nint)), IcppInstanceType)
            {
                Parameters = { new(IcppInstanceType.Resolve().GenericParameters[0]) }
            });
        {
            ILProcessor? il = op_2_nint.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Call, Type.Properties.First(f => f.Name is PointerPropertyName).GetMethod);
            il.Emit(OC.Ret);
        }

        MethodDefinition op_2_voidPtr = Type.DefineMethod(
            "op_Implicit",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.Static,
            Assembly.ImportRef(typeof(void).MakePointerType()),
            parameterTypes: [new("ins", ParameterAttributes.None, Type)]);

        op_2_voidPtr.Overrides.Add(
            new("op_Implicit", Assembly.ImportRef(typeof(void).MakePointerType()), IcppInstanceType)
            {
                Parameters = { new(IcppInstanceType.Resolve().GenericParameters[0]) }
            });
        {
            ILProcessor? il = op_2_voidPtr.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Call, Type.Properties.First(f => f.Name is PointerPropertyName).GetMethod);
            il.Emit(OC.Ret);
        }
    }
    private async ValueTask GenerateDtorDefinitionAsync(Action<ILProcessor>? action = null)
    {
        if (DestructorGenerated) return;
        DestructorGenerated = true;

        await Task.Run(() =>
        {
            Type.Interfaces.Add(new(Assembly.ImportRef(Assembly.TypeSystem.IDisposable)));
            ILProcessor il;

            var destructInstanceMethod = Type.DefineMethod(
                nameof(ICppInstanceNonGeneric.DestructInstance),
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.Static,
                Assembly.ImportRef(typeof(void)),
                parameterTypes: [new("ptr", ParameterAttributes.None, Assembly.ImportRef(typeof(nint)))]);

            var destructMethod = Type.DefineMethod(
                nameof(ICppInstanceNonGeneric.Destruct),
                MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                Assembly.ImportRef(typeof(void)));
            il = destructMethod.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Call, PointerProperty?.GetMethod ?? throw new Exception("pointer property is null"));
            il.Emit(OC.Call, destructInstanceMethod);
            il.Emit(OC.Ret);

            var disposedValueField = Type.DefineField(
                "disposedValue",
                FieldAttributes.Private,
                Assembly.ImportRef(typeof(bool)));

            var disposeMethodVirtual = Type.DefineMethod(
                nameof(IDisposable.Dispose),
                MethodAttributes.Family |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                Assembly.ImportRef(typeof(void)),
                parameterTypes: [new("disposing", ParameterAttributes.None, Assembly.ImportRef(typeof(bool)))]);
            il = disposeMethodVirtual.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldfld, disposedValueField);
            var instruction_test_IsOwner = il.Create(OC.Ldarg_0);
            il.Emit(OC.Brfalse_S, instruction_test_IsOwner);
            il.Emit(OC.Ret);
            il.Append(instruction_test_IsOwner);
            il.Emit(OC.Call, IsOwnerProperty?.GetMethod ?? throw new Exception("owner property is null"));
            var instruction_disposedValue_equals_true = il.Create(OC.Ldarg_0);
            il.Emit(OC.Brfalse_S, instruction_disposedValue_equals_true);
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Call, destructMethod);
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Call, OwnsMemoryProperty?.GetMethod ?? throw new Exception("ownsMemory property is null"));
            il.Emit(OC.Brtrue_S, instruction_disposedValue_equals_true);
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Call, PointerProperty.GetMethod ?? throw new Exception("pointer property is null"));
            il.Emit(OC.Call, Assembly.ImportRef(typeof(HeapAlloc).GetMethod(nameof(HeapAlloc.Delete))!));
            il.Append(instruction_disposedValue_equals_true);
            il.Emit(OC.Ldc_I4_1);
            il.Emit(OC.Stfld, disposedValueField);
            il.Emit(OC.Ret);

            MethodDefinition disposeMethod = Type.DefineMethod(
                nameof(IDisposable.Dispose),
                MethodAttributes.Public |
                MethodAttributes.Final |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
            Assembly.ImportRef(typeof(void)));
            il = disposeMethod.Body.GetILProcessor();
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Ldc_I4_1);
            il.Emit(OC.Callvirt, disposeMethodVirtual);
            il.Emit(OC.Ldarg_0);
            il.Emit(OC.Call, Assembly.ImportRef(Assembly.TypeSystem.GC.GetMethods().First(t => t.Name is nameof(GC.SuppressFinalize))));
            il.Emit(OC.Ret);

            MethodDefinition method_Finalize = Type.DefineMethod(
            "Finalize",
            MethodAttributes.Family |
            MethodAttributes.HideBySig |
            MethodAttributes.Virtual,
            Assembly.ImportRef(typeof(void)));
            method_Finalize.Overrides.Add(Assembly.ImportRef(Assembly.TypeSystem.Object.GetMethods().First(m => m.Name is "Finalize")));
            il = method_Finalize.Body.GetILProcessor();
            Instruction? try_start = il.Create(OC.Ldarg_0);
            Instruction? finally_start = il.Create(OC.Ldarg_0);
            Instruction? finally_end = il.Create(OC.Ret);
            Instruction? try_leave = il.Create(OC.Leave_S, finally_end);
            il.Append(try_start);
            il.Emit(OC.Ldc_I4_0);
            il.Emit(OC.Callvirt, disposeMethodVirtual);
            il.Append(try_leave);
            il.Append(finally_start);
            il.Emit(OC.Call, Assembly.ImportRef(Assembly.TypeSystem.Object.GetMethods().First(m => m.Name is "Finalize")));
            il.Append(finally_end);

            ExceptionHandler handler = new(ExceptionHandlerType.Finally)
            {
                TryStart = try_start,
                TryEnd = try_leave.Next,
                HandlerStart = finally_start,
                HandlerEnd = finally_end
            };
            method_Finalize.Body.ExceptionHandlers.Add(handler);

            il = destructInstanceMethod.Body.GetILProcessor();
            if (action is not null)
            {
                action(il);
            }
            else
            {
                il.Emit(OC.Ret);
            }
        });
    }
}
