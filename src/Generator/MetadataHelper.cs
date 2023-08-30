//flower Q
using Hosihikari.Utils;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.Metadata;
using System.Reflection;

namespace Hosihikari.Generation.Generator;

public static class MetadataHelper
{
    private static readonly Guid guid = new("7DB709E5-8DDF-8730-685F-9A183F655374");
    private static readonly BlobContentId contentId = new(guid, 0x04030201);

    public static void BuildAssembly(string path, in OriginalData originalData)
    {
        FileInfo fileInfo = new(path);
        using var stream = new FileStream(fileInfo.Name, FileMode.OpenOrCreate, FileAccess.ReadWrite);

        var ilBuilder = new BlobBuilder();
        var metadataBuilder = new MetadataBuilder();


    }

    private static void EmitAssembly(string name, BlobBuilder ilBuilder, MetadataBuilder metadata, in OriginalData originalData)
    {
        metadata.AddModule(
            0,
            metadata.GetOrAddString($"{name}.dll"),
            metadata.GetOrAddGuid(guid),
            default,
            default);

        metadata.AddAssembly(
                metadata.GetOrAddString(name),
                version: new Version(1, 0, 0, 0),
                culture: default,
                publicKey: default,
                flags: 0,
                hashAlgorithm: AssemblyHashAlgorithm.None);
    }

    private static T ThrowIfNull<T>(T? value) => value ?? throw new NullReferenceException();

    private static T NullWhen<T>(T? value, Func<T> func) => value ?? func();

    private static T DefaultIfNull<T>(T? value) => value ?? default!;

    private static Type[] ToTypeArray(IReadOnlyList<ParameterInfo> parameters)
    {
        var @params = new Type[parameters.Count];
        for (int i = 0; i < parameters.Count; i++)
            @params[i] = parameters[i].ParameterType;
        return @params;
    }


    private static readonly Dictionary<Assembly, EntityHandle> AssemblyReferences = new();
    private static readonly Dictionary<Type, EntityHandle> TypeReferences = new();
    private static readonly Dictionary<MethodInfo, EntityHandle> MethodReferences = new();

    private static AssemblyReferenceHandle BuildAssemblyReference(MetadataBuilder metadata, Assembly assembly)
    {
        var assemblyName = assembly.GetName();

        var ret = metadata.AddAssemblyReference(
            name: metadata.GetOrAddString(ThrowIfNull(assemblyName.Name)),
            version: ThrowIfNull(assemblyName.Version),
            culture: metadata.GetOrAddString(DefaultIfNull(assemblyName.CultureName)),
            publicKeyOrToken: metadata.GetOrAddBlob(
                NullWhen(assemblyName.GetPublicKey(),
                    () => NullWhen(assemblyName.GetPublicKeyToken(),
                        Array.Empty<byte>))),
            flags: (AssemblyFlags)assemblyName.Flags,
            hashValue: default);

        AssemblyReferences.Add(assembly, ret);
        return ret;
    }

    private static EntityHandle GetOrCreateAssemblyReference(MetadataBuilder metadata, Assembly assembly)
    {
        if (AssemblyReferences.TryGetValue(assembly, out EntityHandle reference) is false)
            reference = BuildAssemblyReference(metadata, assembly);
        return reference;
    }

    private static TypeReferenceHandle BuildTypeReference(MetadataBuilder metadata, Type type)
    {
        var assembly = type.Assembly;
        EntityHandle reference = GetOrCreateAssemblyReference(metadata, assembly);

        var ret = metadata.AddTypeReference(
            reference,
            type.Namespace is null ? default : metadata.GetOrAddString(type.Namespace),
            metadata.GetOrAddString(type.Name));

        TypeReferences.Add(type, ret);
        return ret;
    }

    private static EntityHandle GetOrCreateTypeReference(MetadataBuilder metadata, Type type)
    {

        if (TypeReferences.TryGetValue(type, out EntityHandle reference) is false)
            reference = BuildTypeReference(metadata, type);
        return reference;
    }

    private static BlobBuilder BuildMethodSignature(MetadataBuilder metadata, Type returnType, Type[] parameters, bool isStatic)
    {
        var signature = new BlobBuilder();
        new BlobEncoder(signature)
            .MethodSignature(isInstanceMethod: isStatic is false)
            .Parameters(
            parameterCount: parameters.Length,
            retType =>
            {
                retType.Type().Type(GetOrCreateTypeReference(metadata, returnType), returnType.IsValueType);
            },
            @params =>
            {
                foreach (var parameter in parameters)
                {
                    @params
                    .AddParameter()
                    .Type()
                    .Type(
                        GetOrCreateTypeReference(metadata, parameter),
                        parameter.IsValueType);
                }
            });
        return signature;
    }

    private static MemberReferenceHandle BuildMethodReference(MetadataBuilder metadata, MethodInfo method)
    {
        if (method.IsGenericMethod)
            throw new NotSupportedException();

        var type = ThrowIfNull(method.DeclaringType);
        var typeReference = GetOrCreateTypeReference(metadata, type);

        var methodParameters = ToTypeArray(method.GetParameters());

        var signature = BuildMethodSignature(metadata, method.ReturnType, methodParameters, method.IsStatic);

        var ret = metadata.AddMemberReference(
            typeReference,
            metadata.GetOrAddString(method.Name),
            metadata.GetOrAddBlob(signature));

        MethodReferences.Add(method, ret);
        return ret;
    }

    private static EntityHandle GetOrCreateMethodReference(MetadataBuilder metadata, MethodInfo method)
    {

        if (MethodReferences.TryGetValue(method, out EntityHandle reference) is false)
            reference = BuildMethodReference(metadata, method);
        return reference;
    }

    private static MethodDefinitionHandle BuildMethodDefinitionAndMethodBody(
        MetadataBuilder metadata,
        BlobBuilder ilBuilder,
        MethodAttributes attributes,
        MethodImplAttributes implAttributes,
        string name,
        bool isStatic,
        Type returnType,
        Type[] parameters,
        Action<InstructionEncoder> ilEmitAction)
    {
        var codeBuilder = new BlobBuilder();
        InstructionEncoder il = new(codeBuilder);

        ilEmitAction(il);

        var methodBodyStream = new MethodBodyStreamEncoder(ilBuilder);
        int bodyOffset = methodBodyStream.AddMethodBody(il);
        codeBuilder.Clear();

        var signature = BuildMethodSignature(metadata, returnType, parameters, isStatic);
        return metadata.AddMethodDefinition(
            attributes,
            implAttributes,
            metadata.GetOrAddString(name),
            metadata.GetOrAddBlob(signature),
            bodyOffset,
            default);
    }

    private static BlobBuilder BuildFieldSignature(MetadataBuilder metadata, Type type)
    {
        var signature = new BlobBuilder();
        new BlobEncoder(signature)
            .FieldSignature()
            .Type(GetOrCreateTypeReference(metadata, type), type.IsValueType);
        return signature;
    }

    private static FieldDefinitionHandle BuildFieldDefinition(MetadataBuilder metadata, FieldAttributes attributes, string name, Type type)
    {
        return metadata.AddFieldDefinition(attributes,
            metadata.GetOrAddString(name),
            metadata.GetOrAddBlob(BuildFieldSignature(metadata, type)));
    }
}
