using Hosihikari.Generation.CppParser;
using Hosihikari.Generation.Utils;
using Mono.Cecil;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mono.Cecil.Rocks;

namespace Hosihikari.Generation.ILGenerate;

public class TypeSystem
{
    public TypeSystem(AssemblyDefinition assemblyDefinition)
    {
        var types = assemblyDefinition.MainModule.Types;
        Object = types.FirstOrDefault(t => t.Name == "Object") ?? throw new Exception("Object type is not found");
        String = types.FirstOrDefault(t => t.Name == "String") ?? throw new Exception("String type is not found");
        IDisposable = types.FirstOrDefault(t => t.Name == "IDisposable") ?? throw new Exception("IDisposable type is not found");
        GC = types.FirstOrDefault(t => t.Name == "GC") ?? throw new Exception("GC type is not found");
        ValueType = types.FirstOrDefault(t => t.Name == "ValueType") ?? throw new Exception("ValueType type is not found");
        TargetFrameworkAttribute = types.FirstOrDefault(t => t.Name == "TargetFrameworkAttribute") ?? throw new Exception("TargetFrameworkAttribute type is not found");
    }

    public TypeDefinition Object { get; }
    public TypeDefinition String { get; }
    public TypeDefinition IDisposable { get; }
    public TypeDefinition GC { get; }
    public TypeDefinition ValueType { get; }

    public TypeDefinition TargetFrameworkAttribute { get; }
}

public class AssemblyGenerator
{

    private OriginalData OriginalData { get; }

    public AssemblyDefinition AssemblyDefinition { get; }
    public ModuleDefinition MainModule { get; }

    public ModuleDefinition Module => MainModule;

    public TypeRegistry TypeRegistry { get; }

    private AssemblyDefinition SystemRuntimeAssembly { get; }

    public TypeSystem TypeSystem { get; }

    public IReadOnlyCollection<Assembly> ReferenceAssemblies = [];

    private AssemblyGenerator(OriginalData originalData, AssemblyName assemblyName, string runtimeAssemblyPath, IReadOnlyList<Assembly> referenceAssemblies)
    {
        SystemRuntimeAssembly = AssemblyDefinition.ReadAssembly(runtimeAssemblyPath);
        TypeSystem = new(SystemRuntimeAssembly);

        OriginalData = originalData;
        AssemblyDefinition = AssemblyDefinition.CreateAssembly(
            assemblyName: new(assemblyName.Name, assemblyName.Version),
            moduleName: "MainModule",
            kind: ModuleKind.Dll);

        MainModule = AssemblyDefinition.MainModule;

        AssemblyDefinition.CustomAttributes.Add(
            new(
                constructor: ImportRef(
                    TypeSystem
                    .TargetFrameworkAttribute
                    .GetConstructors()
                    .First(c => c.Parameters.Count is 1)))
            {
                ConstructorArguments =
                {
                    new(ImportRef(typeof(string)), ".NETCoreApp,Version=v8.0")
                },
                Properties =
                {
                    new Mono.Cecil.CustomAttributeNamedArgument(
                        name: "FrameworkDisplayName",
                        argument: new(ImportRef(typeof(string)), ".NET 8.0"))
                }
            });

        TypeRegistry = new(this, referenceAssemblies);
    }

    public static bool TryCreateGenerator(OriginalData originalData, AssemblyName assemblyName, string runtimeAssemblyPath, IReadOnlyList<Assembly> referenceAssemblies, out AssemblyGenerator? generator)
    {
        generator = new(originalData, assemblyName, runtimeAssemblyPath, referenceAssemblies);
        return true;
    }

    private bool TryFindType(string? typeFullName, [NotNullWhen(true)] out TypeDefinition? typeDefinition)
    {
        typeDefinition = null;
        if (string.IsNullOrWhiteSpace(typeFullName))
            return false;
        typeDefinition = SystemRuntimeAssembly.MainModule.Types.FirstOrDefault(t => t?.FullName == typeFullName, null);
        return typeDefinition is not null;
    }

    public TypeReference ImportRef(Type type)
    {
        if (TryFindType(type.FullName ?? "", out TypeDefinition? ret))
            return MainModule.ImportReference(ret);
        else
            return MainModule.ImportReference(type);
    }

    public TypeReference ImportRef(TypeDefinition type)
    {
        return MainModule.ImportReference(type);
    }

    public MethodReference ImportRef(MethodBase method)
    {
        return MainModule.ImportReference(method);
    }

    public MethodReference ImportRef(MethodReference method)
    {
        return MainModule.ImportReference(method);
    }

    public async ValueTask<bool> GenerateAsync()
    {
        try
        {
            foreach (var (typeStr, @class) in OriginalData.Classes)
            {
                if (CppTypeParser.TryParse(typeStr, out var cppType) is false || cppType.RootType.IsTemplate)
                    continue;

                var generator = await TypeRegistry.GetOrRegisterTypeAsync(cppType, @class);
                if (generator is null)
                    continue;

                if (generator.IsEmpty)
                    generator.SetOriginalClass(@class);

                await generator.GenerateAsync();
            }

            await TypeRegistry.GenerateAllTypes();

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async ValueTask SaveAsync(FileInfo file)
        => await Task.Run(() =>
        {
            Module.AssemblyReferences.Remove(Module.AssemblyReferences.First(a => a.Name is "System.Private.CoreLib"));
            AssemblyDefinition.Write(file.FullName);
        });
}