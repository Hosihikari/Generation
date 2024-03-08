namespace Hosihikari.Generation.CppParser;

public class CppType
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CppType"/> class.
    /// </summary>
    public CppType() => Parent = RootType = this;

    public CppType(
        CppFundamentalType? fundamentalType,
        string[]? namespaces,
        string? originalTypeString,
        CppType rootType,
        CppType? subType,
        CppType? parent,
        CppType[]? templateTypes,
        CppTypeEnum type,
        string? typeIdentifier,
        string? typeIdentifierWithTemplateArgs,
        bool isConst)
    {
        FundamentalType = fundamentalType;
        Namespaces = namespaces;
        OriginalTypeString = originalTypeString;
        RootType = rootType;
        SubType = subType;
        Parent = parent;
        TemplateTypes = templateTypes;
        Type = type;
        TypeIdentifier = typeIdentifier;
        TypeIdentifierWithTemplateArgs = typeIdentifierWithTemplateArgs;
        IsConst = isConst;
    }

    /// <summary>
    /// Gets or sets the fundamental type.
    /// </summary>
    public CppFundamentalType? FundamentalType;

    /// <summary>
    /// Gets or sets the namespaces.
    /// </summary>
    public string[]? Namespaces;

    /// <summary>
    /// Gets or sets the original type string.
    /// </summary>
    public string? OriginalTypeString;

    /// <summary>
    /// Gets or sets the root type.
    /// </summary>
    public CppType RootType;

    /// <summary>
    /// Gets or sets the sub type.
    /// </summary>
    public CppType? SubType;

    /// <summary>
    /// Gets or sets the parent.
    /// </summary>
    public CppType? Parent;

    /// <summary>
    /// Gets or sets the template types.
    /// </summary>
    public CppType[]? TemplateTypes;

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public CppTypeEnum Type;

    /// <summary>
    /// Gets or sets the type identifier.
    /// </summary>
    public string? TypeIdentifier;

    /// <summary>
    /// Gets or sets the type identifier with template arguments.
    /// </summary>
    public string? TypeIdentifierWithTemplateArgs;

    /// <summary>
    /// Gets or sets a value indicating whether the type is a const type.
    /// </summary>
    public bool IsConst;

    /// <summary>
    /// Gets a value indicating whether the type is a fundamental type.
    /// </summary>
    public bool IsFundamentalType => FundamentalType is not null;

    /// <summary>
    /// Gets a value indicating whether the type is a template type.
    /// </summary>
    public bool IsTemplate => TemplateTypes is not null;

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return Type switch
        {
            CppTypeEnum.FundamentalType => FundamentalType.ToString()!,
            CppTypeEnum.Pointer => "*",
            CppTypeEnum.Ref => "&",
            CppTypeEnum.Array => "[]",
            CppTypeEnum.Enum => $"enum {string.Join("::", Namespaces ?? [])} {TypeIdentifier}",
            CppTypeEnum.Class => $"class {string.Join("::", Namespaces ?? [])} {TypeIdentifier}",
            CppTypeEnum.Struct => $"struct {string.Join("::", Namespaces ?? [])} {TypeIdentifier}",
            CppTypeEnum.Union => $"union {string.Join("::", Namespaces ?? [])} {TypeIdentifier}",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Performs the specified action on each element of the <see cref="CppType"/>.
    /// </summary>
    /// <param name="action">The <see cref="Action{T1, T2, T3}"/> to perform on each element.</param>
    public void ForEach(Action<CppType, int, bool> action)
    {
        CppType? current = this;
        int index = 0;
        while (current is not null)
        {
            action(current, index, current.SubType is null);
            index++;
            current = current.SubType;
        }
    }

    /// <summary>
    /// Returns an <see cref="IEnumerable{T}"/> that contains the elements of the <see cref="CppType"/>.
    /// </summary>
    /// <returns>An <see cref="IEnumerable{T}"/> that contains the elements of the <see cref="CppType"/>.</returns>
    public IEnumerable<CppType> ToEnumerable()
    {
        List<CppType> nodes = [];
        ForEach((node, _, _) => nodes.Add(node));
        return [.. nodes];
    }

    public IEnumerable<CppType> ToEnumerableReversed() => ToEnumerable().Reverse();
}
