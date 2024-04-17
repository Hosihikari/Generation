global using OC = System.Reflection.Emit.OpCodes;

using Hosihikari.Generation.CppParser;
using Hosihikari.Generation.Utils;
using Hosihikari.NativeInterop;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Hosihikari.Generation.ILGenerate;

public class MethodGenerator
{
    public const string CtorPrefix = "ctor_";
    public const string DtorPrefix = "dtor_";
    public const string FuncPrefix = "func_";

    public const string FunctionPointerName = "FunctionPointer";

    public TypeGenerator TypeGenerator { get; }

    public AccessType AccessType { get; }

    public bool IsStatic { get; }

    public OriginalItem MethodItem { get; }

    public CppType ReturnType { get; }
    public CppType[] Parameters { get; }

    public MethodBuilder? MethodBuilder { get; private set; }

    private MethodGenerator(AccessType accessType, bool isStatic, TypeGenerator typeGenerator, OriginalItem item, CppType returnType, CppType[] parameters)
    {
        TypeGenerator = typeGenerator;
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

        if (CppTypeParser.TryParse(item.Type.Name, out var returnType) is false)
            return false;

        CppType[] parameters = item.Params is not null ? new CppType[item.Params.Length] : [];

        if (item.Params is not null)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if (CppTypeParser.TryParse(item.Params[i].Name, out var parameterType) is false)
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
    [MemberNotNullWhen(true, nameof(MethodBuilder))]
    public async ValueTask<bool> GenerateAsync()
    {
        // Get the type registry from the AssemblyGenerator
        var registry = TypeGenerator.AssemblyGenerator.TypeRegistry;

        // Resolve the return type asynchronously
        Type? returnType = await registry.ResolveTypeAsync(ReturnType);

        // Resolve parameter types asynchronously
        Type?[] parameterTypes = new Type[Parameters.Length];
        for (int i = 0; i < Parameters.Length; i++)
        {
            var temp = await registry.ResolveTypeAsync(Parameters[i]);
            parameterTypes[i] = temp;
        }

        // Check if the return type or any parameter type is null
        if (returnType is null || parameterTypes.Any(x => x is null))
        {
            await GenerateFunctionPointer(); // Generate function pointer if types are not resolved
            return false;
        }
        else
        {
            return await GenerateMethod(); // Generate method if types are resolved
        }
    }


    public async ValueTask<FieldBuilder> GenerateFunctionPointer()
    {
        // Generate the function pointer asynchronously
        return await Task.Run(() =>
        {
            // Get the type registry and original type builder
            var registry = TypeGenerator.AssemblyGenerator.TypeRegistry;
            var original = TypeGenerator.OriginalTypeBuilder;

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
                builder.Append(char.IsLetter(c) || c is '_' ? c : '_');
            }

            // Define the nested type for the function pointer
            var type = original.DefineNestedType(
                name: builder.ToString(),
                attr:
                    TypeAttributes.NestedPrivate |
                    TypeAttributes.Sealed |
                    TypeAttributes.Abstract |
                    TypeAttributes.Class);

            // Define the field for the function pointer
            var field = type.DefineField(FunctionPointerName, typeof(nint), FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly);

            // Define the constructor for the type
            var cctor = type.DefineConstructor(
                attributes:
                    MethodAttributes.Static |
                    MethodAttributes.Private |
                    MethodAttributes.HideBySig |
                    MethodAttributes.SpecialName |
                    MethodAttributes.RTSpecialName,
                callingConvention: CallingConventions.Standard,
                parameterTypes: null);

            // Get the ILGenerator for the constructor
            var il = cctor.GetILGenerator();

            // Emit IL instructions to initialize the field with the symbol value
            il.Emit(OC.Ldstr, MethodItem.Symbol);
            il.EmitCall(OC.Call, typeof(SymbolHelper).GetMethod(nameof(SymbolHelper.DlsymPointer))!, null);
            il.Emit(OC.Stsfld, field);
            il.Emit(OC.Ret);

            // Return the field builder
            return field;
        });
    }

    public async ValueTask<bool> GenerateMethod()
    {
        throw new NotImplementedException();
    }
}