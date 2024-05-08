using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Hosihikari.Generation.ILGenerate;

public static class CecilExtension
{
    public static TypeDefinition DefineType(this ModuleDefinition declaringModule, string @namespace, string name, TypeAttributes attributes)
    {
        var type = new TypeDefinition(@namespace, name, attributes);
        declaringModule.Types.Add(type);
        return type;
    }

    public static TypeDefinition DefineType(this TypeDefinition declaringType, string @namespace, string name, TypeAttributes attributes, TypeReference? baseType = null)
    {
        var type = baseType is null ? 
            new TypeDefinition(@namespace, name, attributes) :
            new TypeDefinition(@namespace, name, attributes, baseType);
        declaringType.NestedTypes.Add(type);
        return type;
    }

    public static FieldDefinition DefineField(this TypeDefinition declaringType, string name, FieldAttributes attributes, TypeReference fieldType)
    {
        var field = new FieldDefinition(name, attributes, fieldType);
        declaringType.Fields.Add(field);
        return field;
    }

    public static MethodDefinition DefineMethod(this TypeDefinition declaringType, string name, MethodAttributes attributes, TypeReference returnType, params ParameterDefinition[] parameterTypes)
    {
        var method = new MethodDefinition(name, attributes, returnType);
        foreach (var parameter in parameterTypes)
            method.Parameters.Add(parameter);
        declaringType.Methods.Add(method);
        return method;
    }

    public static MethodDefinition DefineMethod(this TypeDefinition declaringType, string name, MethodAttributes attributes, TypeReference returnType, IEnumerable<ParameterDefinition> parameterTypes)
    {
        var method = new MethodDefinition(name, attributes, returnType);
        foreach (var parameter in parameterTypes)
            method.Parameters.Add(parameter);
        declaringType.Methods.Add(method);
        return method;
    }

    public static MethodDefinition DefineMethod(
            this TypeDefinition declaringType,
            string name,
            MethodAttributes attributes,
            TypeReference returnType,
            TypeReference[] parameterTypes,
            string[]? parameterNames = null,
            ParameterAttributes[]? parameterAttributes = null)
    {
        // Create a new MethodDefinition with the given name, attributes, and return type
        var method = new MethodDefinition(name, attributes, returnType);

        // Loop through each parameter type
        foreach (var parameter in parameterTypes)
        {
            // Create a new ParameterDefinition with the given parameter type
            var parameterDefinition = new ParameterDefinition(parameter);

            // Add the parameter definition to the method's parameters
            method.Parameters.Add(parameterDefinition);

            // If parameter names are provided, set the parameter definition's name to the corresponding name
            if (parameterNames is not null)
                parameterDefinition.Name = parameterNames[parameterDefinition.Index];

            // If parameter attributes are provided, set the parameter definition's attributes to the corresponding attributes
            if (parameterAttributes is not null)
                parameterDefinition.Attributes = parameterAttributes[parameterDefinition.Index];
        }

        // Add the method to the declaring type's methods
        declaringType.Methods.Add(method);

        // Return the MethodDefinition of the defined method
        return method;
    }

    public static PropertyDefinition DefineProperty(
        this TypeDefinition declaringType,
        string name,
        PropertyAttributes attributes,
        TypeReference type)
    {
        var property = new PropertyDefinition(name, attributes, type);
        declaringType.Properties.Add(property);
        return property;
    }

    public static PropertyDefinition BindMethods(
        this PropertyDefinition property,
        MethodDefinition? getMethod = null,
        MethodDefinition? setMethod = null)
    {
        if (getMethod is null && setMethod is null)
            return property;

        if (getMethod is not null)
            property.GetMethod = getMethod;

        if (setMethod is not null)
            property.SetMethod = setMethod;

        return property;
    }


    public static void EmitCalli(this ILProcessor il, MethodCallingConvention callingConvention, TypeReference returnType, params ParameterDefinition[] parameterTypes)
    {
        var callSite = new CallSite(returnType)
        {
            CallingConvention = callingConvention,
        };
        foreach (var parameter in parameterTypes)
            callSite.Parameters.Add(parameter);
        il.Emit(OC.Calli, callSite);
    }

    public static void LoadThis(this ILProcessor il)
    {
        if (il.Body.Method.HasThis)
            il.Emit(OC.Ldarg, 0);
    }

    public static void LoadAllArgs(this ILProcessor il)
    {
        for (int i = 0; i < il.Body.Method.Parameters.Count; i++)
        {
            il.Emit(OC.Ldarg, i + (il.Body.Method.HasThis ? 1 : 0));
        }
    }

    public static ParameterDefinition[] CreateParamDefinitions(TypeReference[] typeReferences, Action<ParameterDefinition>? action = null)
    {
        var parameters = new ParameterDefinition[typeReferences.Length];
        for (int i = 0; i < typeReferences.Length; i++)
        {
            var parameter = new ParameterDefinition(typeReferences[i]);
            parameters[i] = parameter;
            action?.Invoke(parameter);
        }
        return parameters;
    }

    public static ParameterDefinition Clone(this ParameterDefinition parameter)
    {
        return new ParameterDefinition(parameter.Name, parameter.Attributes, parameter.ParameterType);
    }
}
