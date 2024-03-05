using System.Text.Json.Serialization;

namespace Hosihikari.Generation.Utils;

public record OriginalData(
    [property: JsonPropertyName("classes")]
    Dictionary<string, Class> Classes,
    [property: JsonPropertyName("identifier")]
    IdentifierData Identifier,
    [property: JsonPropertyName("sha_256_hash")]
    Hashs Sha256Hash);

public record Class(
    [property: JsonPropertyName("parent_types")]
    string[]? ParentTypes,
    [property: JsonPropertyName("vtbl_entry")]
    string[]? VtblEntry,
    [property: JsonPropertyName("child_types")]
    string[]? ChildTypes,
    [property: JsonPropertyName("public")] Item[]? Public,
    [property: JsonPropertyName("protected")]
    Item[]? Protected,
    [property: JsonPropertyName("private")]
    Item[]? Private,
    [property: JsonPropertyName("public.static")]
    Item[]? PublicStatic,
    [property: JsonPropertyName("protected.static")]
    Item[]? ProtectedStatic,
    [property: JsonPropertyName("private.static")]
    Item[]? PrivateStatic,
    [property: JsonPropertyName("virtual")]
    Item[]? Virtual,
    [property: JsonPropertyName("virtual.unordered")]
    Item[]? VirtualUnordered);

public record IdentifierData(
    [property: JsonPropertyName("class")] string[] Class,
    [property: JsonPropertyName("struct")] string[] Struct);

public record Hashs(
    [property: JsonPropertyName("bedrock_server.exe")]
    string Exe,
    [property: JsonPropertyName("bedrock_server.pdb")]
    string Pdb,
    [property: JsonPropertyName("bedrock_server_symbols.debug")]
    string Elf);

public record Item(
    [property: JsonPropertyName("access_type")]
    int AccessType,
    [property: JsonPropertyName("fake_symbol")]
    string FakeSymbol,
    [property: JsonPropertyName("flag_bits")]
    int FlagBits,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("namespace")]
    string Namespace,
    [property: JsonPropertyName("params")] TypeData[]? Params,
    [property: JsonPropertyName("rva")] ulong Rva,
    [property: JsonPropertyName("storage_class")]
    int StorageClass,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("symbol_type")]
    int SymbolType,
    [property: JsonPropertyName("type")] TypeData Type);

public record TypeData(
    [property: JsonPropertyName("kind")] int Kind,
    [property: JsonPropertyName("name")] string Name);