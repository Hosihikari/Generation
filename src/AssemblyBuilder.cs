using Hosihikari.Generation.Generator;
using Hosihikari.Utils;
using Mono.Cecil;

namespace Hosihikari.Generation;

public class AssemblyBuilder
{
    private readonly string name;
    private readonly AssemblyDefinition assembly;
    private readonly ModuleDefinition module;
    private readonly string outputDir;
    private readonly Dictionary<string, TypeDefinition> definedTypes;

    public AssemblyBuilder(AssemblyDefinition assembly, string outputDir, string name)
    {
        this.assembly = assembly;
        module = assembly.MainModule;
        this.outputDir = outputDir;
        definedTypes = new();
        this.name = name;

        InsertAttributes();
    }

    public static AssemblyBuilder Create(string name, Version version, string outputDir, string? moduleName = null)
    {
        var assemblyDef = AssemblyDefinition.CreateAssembly(new(name, version), moduleName ?? name, ModuleKind.Dll);
        return new(assemblyDef, outputDir, name);
    }

    private void InsertAttributes()
    {
        module.Runtime = TargetRuntime.Net_4_0;

        CustomAttributeArgument arg = new(module.TypeSystem.String, ".NETCoreApp,Version=v7.0");
        CustomAttributeArgument frameworkDisplayName = new(module.TypeSystem.String, ".NET 7.0");
        CustomAttributeNamedArgument namedArgument = new("FrameworkDisplayName", frameworkDisplayName);

        CustomAttribute attribute = new(
            module.ImportReference(
                typeof(System.Runtime.Versioning.TargetFrameworkAttribute)
                .GetConstructors()
                .First(c => c.GetParameters().Length is 1)));

        attribute.ConstructorArguments.Add(arg);
        attribute.Properties.Add(namedArgument);

        assembly.CustomAttributes.Add(attribute);
    }

    public void Build(in OriginalData data)
    {
        foreach (var @class in data.Classes)
        {
            var arr = new List<OriginalData.Class.Item>?[]
            {
                @class.Value.Public,
                @class.Value.Protected,
                @class.Value.PublicStatic,
                @class.Value.PrivateStatic,
                @class.Value.Virtual
            };

            foreach (var list in arr)
            {
                if (list is null) continue;

                foreach (var item in list)
                {
                    try
                    {

                        if (string.IsNullOrEmpty(item.Type.Name) is false)
                        {
                            var typeData = new TypeData(item.Type);
                            if (definedTypes.ContainsKey(typeData.FullTypeIdentifier) is false)
                            {
                                if (typeData.TryInsertTypeDefinition(module, out var type))
                                    definedTypes.Add(typeData.FullTypeIdentifier, type);
                            }
                        }

                        if (item.Params is not null)
                        {
                            foreach (var param in item.Params)
                            {
                                var typeData = new TypeData(param);
                                if (definedTypes.ContainsKey(typeData.FullTypeIdentifier) is false)
                                {
                                    if (typeData.TryInsertTypeDefinition(module, out var type))
                                        definedTypes.Add(typeData.FullTypeIdentifier, type);
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
        }
    }

    public void Wirte()
    {
        assembly.Write(Path.Combine(outputDir, $"{name}.dll"));
    }

    public AssemblyBuilder AddOrGetType(in TypeData type)
    {
        if (definedTypes.ContainsKey(type.FullTypeIdentifier) is false)
        {
            TypeAttributes typeAttributes = TypeAttributes.Class;
            typeAttributes |= type.Namespaces.Count is 0 ? TypeAttributes.Public : TypeAttributes.NestedPublic;

            var typeDef = new TypeDefinition(string.Empty, type.TypeIdentifier, typeAttributes);
            definedTypes.Add(type.FullTypeIdentifier, typeDef);
        }

        return this;
    }
}
