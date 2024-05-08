using Hosihikari.Generation.Utils;
using Hosihikari.NativeInterop.Unmanaged;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Diagnostics.CodeAnalysis;

namespace Hosihikari.Generation.ILGenerate;

public class VTableGenerator(OriginalVtable vtable, TypeGenerator type)
{
    public OriginalVtable Vtable { get; } = vtable;

    public TypeGenerator DeclaringType { get; } = type;

    public TypeDefinition? VtableStructType { get; private set; } = null!;

    private AssemblyGenerator Assembly => DeclaringType.Assembly;

    public static bool TryCreateGenerator(OriginalVtable vtable, TypeGenerator typeGenerator, [NotNullWhen(true)] out VTableGenerator? generator)
    {
        generator = new(vtable, typeGenerator);
        return true;
    }


    [MemberNotNull(nameof(VtableStructType))]
    public async ValueTask<bool> GenerateAsync()
    {
        VtableStructType = DeclaringType.Type.DefineType(
            string.Empty,
            $"Vftable_{Vtable.Offset}",
            TypeAttributes.NestedPublic |
            TypeAttributes.Sealed |
            TypeAttributes.AnsiClass |
            TypeAttributes.BeforeFieldInit |
            TypeAttributes.SequentialLayout,
            Assembly.ImportRef(Assembly.TypeSystem.ValueType));

        await GeneratePropertyAsync();

        foreach (var (index, originalMethod) in Vtable.Functions.Index())
        {
            if (string.IsNullOrWhiteSpace(originalMethod.Name) is false &&
                string.IsNullOrWhiteSpace(originalMethod.Symbol) is false &&
                MethodGenerator.TryCreateMethodGenerator(
                    AccessType.Public,
                    false,
                    originalMethod,
                    DeclaringType,
                    out var methodGenerator))
            {
                var (success, returnType, parameterTypes, hasVarArgs) = await methodGenerator.CheckTypes();
                if (success)
                {
                    var fptr = await methodGenerator.GenerateFunctionPointer();
                    var original = methodGenerator.GenerateOriginalMethod(returnType!, parameterTypes!, fptr, hasVarArgs);

                    var fptrType = new FunctionPointerType()
                    {
                        CallingConvention = MethodCallingConvention.C,
                        ReturnType = returnType
                    };
                    foreach (var parameter in original.Parameters)
                        fptrType.Parameters.Add(parameter);
                    if (original.CallingConvention.HasFlag(MethodCallingConvention.VarArg))
                        fptrType.Parameters.Add(new(Assembly.ImportRef(typeof(RuntimeArgumentHandle))));

                    var field = VtableStructType.DefineField(
                        $"{original.Name}_{index}",
                        FieldAttributes.Public | FieldAttributes.InitOnly,
                        fptrType);

                    await GenerateMethodAsync(originalMethod, Vtable.Offset, original, field, hasVarArgs);
                }
                else
                {
                    VtableStructType.DefineField(
                        $"unknown_{index}",
                        FieldAttributes.Public | FieldAttributes.InitOnly,
                        Assembly.ImportRef(typeof(nint)));
                }
            }
        }
        return true;
    }

    private async ValueTask GenerateMethodAsync(OriginalItem item, int vtableOffset, MethodDefinition original, FieldDefinition vfptrField, bool hasVarAgs)
    {
        var method = DeclaringType.Type.DefineMethod(
            item.GetMethodNameUpper(),
            MethodAttributes.Public,
            original.ReturnType,
            from param in original.Parameters.Index().Skip(1)
            let paramNames = item.ParameterNames ?? []
            let paramName = param.Index < paramNames.Count ? paramNames[param.Index] : string.Empty
            select new ParameterDefinition(
                paramName,
                param.Item.Attributes,
                param.Item.ParameterType));
        if (hasVarAgs) method.CallingConvention |= MethodCallingConvention.VarArg;
        var vtablePtr = new VariableDefinition(VtableStructType.MakePointerType());
        method.Body.Variables.Add(vtablePtr);
        var il = method.Body.GetILProcessor();
        il.LoadThis();
        il.Emit(OC.Call, DeclaringType.PointerProperty?.GetMethod ?? throw new Exception("pointer property is null"));
        il.Emit(OC.Ldc_I4, vtableOffset);
        il.Emit(OC.Call, Assembly.ImportRef(typeof(CppTypeSystem).GetMethods().First(m => m.Name is nameof(CppTypeSystem.GetVTable))));
        il.Emit(OC.Stloc, vtablePtr);
        il.LoadThis();
        il.Emit(OC.Call, DeclaringType.PointerProperty?.GetMethod ?? throw new Exception("pointer property is null"));
        il.LoadAllArgs();
        if (hasVarAgs) il.Emit(OC.Arglist);
        il.Emit(OC.Ldloc, vtablePtr);
        il.Emit(OC.Ldfld, vfptrField);
        il.EmitCalli(MethodCallingConvention.C, original.ReturnType, [.. original.Parameters]);
        il.Emit(OC.Ret);

        if ((SymbolType)item.SymbolType is SymbolType.Destructor)
        {
            await DeclaringType.GenerateDtorDefinitionAsync(il =>
            {
                il.LoadThis();
                il.Emit(OC.Call, method);
                il.Emit(OC.Ret);
            });
        }
    }

    private async ValueTask GeneratePropertyAsync()
    => await Task.Run(() =>
    {
        if (VtableStructType is null)
            return;

        ILProcessor? il;

        VtableStructType.Interfaces.Add(new(Assembly.ImportRef(typeof(ICppVtable))));

        var lengthProperty = VtableStructType.DefineProperty(
            nameof(ICppVtable.VtableLength),
            PropertyAttributes.None,
            Assembly.ImportRef(typeof(ulong)));

        var lengthPropertyGetMethod = VtableStructType.DefineMethod(
            $"get_{nameof(ICppVtable.VtableLength)}",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.Static,
            Assembly.ImportRef(typeof(ulong)));
        lengthPropertyGetMethod.Overrides.Add(Assembly.ImportRef(
            typeof(ICppVtable).GetMethods().First(f => f.Name is $"get_{nameof(ICppVtable.VtableLength)}")));
        il = lengthPropertyGetMethod.Body.GetILProcessor();
        il.Emit(OC.Ldc_I8, (long)Vtable.Functions.Count);
        il.Emit(OC.Conv_U8);
        il.Emit(OC.Ret);
        lengthProperty.BindMethods(lengthPropertyGetMethod);


        var offsetProperty = VtableStructType.DefineProperty(
            nameof(ICppVtable.Offset),
            PropertyAttributes.None,
            Assembly.ImportRef(typeof(int)));

        var offsetPropertyGetMethod = VtableStructType.DefineMethod(
            $"get_{nameof(ICppVtable.Offset)}",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.Static,
            Assembly.ImportRef(typeof(int)));
        offsetPropertyGetMethod.Overrides.Add(Assembly.ImportRef(
            typeof(ICppVtable).GetMethods().First(f => f.Name is $"get_{nameof(ICppVtable.Offset)}")));
        il = offsetPropertyGetMethod.Body.GetILProcessor();
        il.Emit(OC.Ldc_I4, Vtable.Offset);
        il.Emit(OC.Ret);
        offsetProperty.BindMethods(offsetPropertyGetMethod);
    });
}
