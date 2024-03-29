using Hosihikari.Generation.CppParser;
using Hosihikari.Generation.Utils;
using System.Reflection.Emit;

namespace Hosihikari.Generation.ILGenerate;

public class MethodGenerator
{

    public TypeGenerator TypeGenerator { get; }

    public OriginalItem Item { get; }

    public CppType ReturnType { get; }
    public CppType[] Parameters { get; }

    public MethodBuilder? MethodBuilder { get; private set; }

    private MethodGenerator(TypeGenerator typeGenerator, OriginalItem item, CppType returnType, CppType[] parameters)
    {
        TypeGenerator = typeGenerator;
        Item = item;
        ReturnType = returnType;
        Parameters = parameters;
    }

    public static bool TryCreateMethodGenerator(OriginalItem item, TypeGenerator typeGenerator, out MethodGenerator? methodGenerator)
    {
        methodGenerator = null;

        if (CppTypeParser.TryParse(item.Type.Name, out var returnType) is false)
            return false;

        CppType[] parameters;
        if (item.Params is not null)
            parameters = new CppType[item.Params.Length];
        else
            parameters = [];

        if (item.Params is not null)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if (CppTypeParser.TryParse(item.Params[i].Name, out var parameterType) is false)
                    return false;

                parameters[i] = parameterType;
            }
        }

        methodGenerator = new MethodGenerator(typeGenerator, item, returnType, parameters);
        return true;
    }
}