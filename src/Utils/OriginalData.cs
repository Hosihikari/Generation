using System.Text.Json.Serialization;

namespace Hosihikari.Generation.Utils;

[Flags]
public enum AccessType
{
    Public,
    Protected,
    Private,
    PublicStatic,
    ProtectedStatic,
    PrivateStatic,
    Virtual,
    VirtualUnordered
}

[Flags]
public enum SymbolType
{
    Function = 0,
    Constructor = 1,
    Destructor = 2,
    Operator = 3,
    StaticField = 4,
    UnknownFunction = 5
}

public static class OriginalDataExtensions
{
    public static OriginalItem[]?[] GetAllItems(this OriginalClass @class) =>
        [
            @class.Public,
            @class.Protected,
            @class.Private,
            @class.PublicStatic,
            @class.ProtectedStatic,
            @class.PrivateStatic,
            @class.Virtual,
            @class.VirtualUnordered
        ];

    public static (AccessType accessType, bool isStatic, OriginalItem[]? item)[] GetAllItemsWithAccessType(this OriginalClass @class) =>
        [
            (AccessType.Public,false, @class.Public),
            (AccessType.Protected,false, @class.Protected),
            (AccessType.Private,false, @class.Private),
            (AccessType.PublicStatic,true, @class.PublicStatic),
            (AccessType.ProtectedStatic,true, @class.ProtectedStatic),
            (AccessType.PrivateStatic,true, @class.PrivateStatic),
            (AccessType.Virtual,false, @class.Virtual),
            (AccessType.VirtualUnordered,false, @class.VirtualUnordered)
        ];

    public static (AccessType accessType, bool isStatic, OriginalItem[]? item)[] GetAllItemsWithAccessTypeExceptedVirtual(this OriginalClass @class) =>
        [
            (AccessType.Public,false, @class.Public),
            (AccessType.Protected,false, @class.Protected),
            (AccessType.Private,false, @class.Private),
            (AccessType.PublicStatic,true, @class.PublicStatic),
            (AccessType.ProtectedStatic,true, @class.ProtectedStatic),
            (AccessType.PrivateStatic,true, @class.PrivateStatic),
            (AccessType.VirtualUnordered,false, @class.VirtualUnordered)
        ];

    public static string GetMethodNameLower(this OriginalItem item)
        => (item.Name.Length > 1 ? $"{char.ToLower(item.Name[0])}{item.Name[1..]}" : item.Name.ToLower()).Replace("~", "Dtor_");

    public static string GetMethodNameUpper(this OriginalItem item)
        => (item.Name.Length > 1 ? $"{char.ToUpper(item.Name[0])}{item.Name[1..]}" : item.Name.ToUpper()).Replace("~", "Dtor_");
}

public record OriginalData(
    [property: JsonPropertyName("classes")]
    Dictionary<string, OriginalClass> Classes,
    [property: JsonPropertyName("identifier")]
    OriginalIdentifierData Identifier,
    [property: JsonPropertyName("sha_256_hash")]
    OriginalHashes Sha256Hash
);

public record OriginalClass(
    [property: JsonPropertyName("parent_types")]
    string[]? ParentTypes,
    [property: JsonPropertyName("vtbl_entry")]
    string[]? VtblEntry,
    [property: JsonPropertyName("child_types")]
    string[]? ChildTypes,
    [property: JsonPropertyName("public")]
    OriginalItem[]? Public,
    [property: JsonPropertyName("protected")]
    OriginalItem[]? Protected,
    [property: JsonPropertyName("private")]
    OriginalItem[]? Private,
    [property: JsonPropertyName("public.static")]
    OriginalItem[]? PublicStatic,
    [property: JsonPropertyName("protected.static")]
    OriginalItem[]? ProtectedStatic,
    [property: JsonPropertyName("private.static")]
    OriginalItem[]? PrivateStatic,
    [property: JsonPropertyName("virtual")]
    OriginalItem[]? Virtual,
    [property: JsonPropertyName("virtual.unordered")]
    OriginalItem[]? VirtualUnordered,

    [property: JsonPropertyName("vtables")]
    List<OriginalVtable>? Vtables
);

public record OriginalIdentifierData(
    [property: JsonPropertyName("class")] string[] Class,
    [property: JsonPropertyName("struct")] string[] Struct
);

public record OriginalHashes(
    [property: JsonPropertyName("bedrock_server.exe")]
    string Exe,
    [property: JsonPropertyName("bedrock_server.pdb")]
    string Pdb,
    [property: JsonPropertyName("bedrock_server_symbols.debug")]
    string Elf
);

public record OriginalItem(
    [property: JsonPropertyName("access_type")]
    int AccessType,
    [property: JsonPropertyName("fake_symbol")]
    string FakeSymbol,
    [property: JsonPropertyName("flag_bits")]
    int FlagBits,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("namespace")]
    string Namespace,
    [property: JsonPropertyName("params")] OriginalTypeData[]? Parameters,
    [property: JsonPropertyName("params_name")] List<string>? ParameterNames,
    [property: JsonPropertyName("rva")] ulong Rva,
    [property: JsonPropertyName("storage_class")]
    int StorageClass,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("symbol_type")]
    int SymbolType,
    [property: JsonPropertyName("type")] OriginalTypeData Type
);

public record OriginalTypeData(
    [property: JsonPropertyName("kind")] int Kind,
    [property: JsonPropertyName("name")] string Name
);

public record OriginalVtable(
    [property: JsonPropertyName("entry")] string Entry,
    [property: JsonPropertyName("functions")] List<OriginalItem> Functions,
    [property: JsonPropertyName("offset")] int Offset
);