using System.Text.Json.Serialization;

namespace Hosihikari.Generation.Utils;

public struct OriginalData
{
    //public static OriginalData GlobalData = JsonSerializer.Deserialize<OriginalData>(File.ReadAllText(Config.OriginalDataPath));

    [JsonPropertyName("classes")] public Dictionary<string, Class> Classes { get; set; }

    public struct Class
    {
        [JsonPropertyName("parent_types")]
        /// <summary>
        /// 有序存储的父类类型，下标最小（0）为最基础的类型
        /// </summary>
        public List<string> ParentTypes { get; set; }

        [JsonPropertyName("vtbl_entry")]
        /// <summary>
        /// 虚表入口，可能有多个
        /// </summary>
        public List<string> VtblEntry { get; set; }

        [JsonPropertyName("child_types")]
        /// <summary>
        /// 无序存储的子类类型
        /// </summary>
        public List<string> ChildTypes { get; set; }

        [JsonPropertyName("public")] public List<Item> Public { get; set; }
        [JsonPropertyName("protected")] public List<Item> Protected { get; set; }
        [JsonPropertyName("private")] public List<Item> Private { get; set; }
        [JsonPropertyName("public.static")] public List<Item> PublicStatic { get; set; }
        [JsonPropertyName("private.static")] public List<Item> PrivateStatic { get; set; }
        [JsonPropertyName("virtual")] public List<Item> Virtual { get; set; }

        [JsonPropertyName("virtual.unordered")]
        public List<Item> VirtualUnordered { get; set; }

        public struct Item
        {
            [JsonPropertyName("access_type")] public int AccessType { get; set; }
            [JsonPropertyName("fake_symbol")] public string FakeSymbol { get; set; }

            [JsonPropertyName("flag_bits")]
            /// <summary>
            ///     Flag
            ///     [0] none
            ///     [1] const
            ///     [2] constructor
            ///     [3] destructor
            ///     [4] operate;
            ///     [5] unknown func
            ///     [6] static global var
            ///     [7] __ptr64 spec (该函数有this指针时会有该标志)
            ///     [8] isPureCall (purecall函数会有该标准)
            /// </summary>
            public int FlagBits { get; set; }

            [JsonPropertyName("name")] public string Name { get; set; }
            [JsonPropertyName("namespace")] public string Namespace { get; set; }
            [JsonPropertyName("params")] public List<TypeData>? Params { get; set; }

            [JsonPropertyName("params_name")] public List<string>? ParamsName { get; set; }

            public struct TypeData
            {
                [JsonPropertyName("kind")] public int Kind { get; set; }
                [JsonPropertyName("name")] public string Name { get; set; }
            }

            [JsonPropertyName("rva")] public ulong RVA { get; set; }
            [JsonPropertyName("storage_class")] public int StorageClass { get; set; }
            [JsonPropertyName("symbol")] public string Symbol { get; set; }

            [JsonPropertyName("symbol_type")]
            /// <summary>
            ///     [1] ctor
            ///     [2] dtor
            ///     [3] operator
            ///     [4] field
            ///     [5] normal function
            /// </summary>
            public int SymbolType { get; set; }

            [JsonPropertyName("type")] public TypeData Type { get; set; }
        }
    }

    [JsonPropertyName("fn_list")] public Dictionary<string, int> FunctionList { get; set; }
    [JsonPropertyName("identifier")] public IdentifierData Identifier { get; set; }

    public struct IdentifierData
    {
        [JsonPropertyName("class")] public List<string> Class { get; set; }
        [JsonPropertyName("struct")] public List<string> Struct { get; set; }
    }

    [JsonPropertyName("sha_256_hash")] public Hashs Sha256Hash { get; set; }

    public struct Hashs
    {
        [JsonPropertyName("bedrock_server.exe")]
        public string Exe { get; set; }

        [JsonPropertyName("bedrock_server.pdb")]
        public string Pdb { get; set; }
    }
}