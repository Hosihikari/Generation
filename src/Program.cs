using Hosihikari.Generation;
using Hosihikari.Generation.Generator;
using Hosihikari.Utils;

var methodData = new MethodData(new Hosihikari.Utils.OriginalData.Class.Item()
{
    AccessType = (int)AccessType.Public,
    FakeSymbol = "",
    FlagBits = 0,
    Name = "testMethodName",
    Namespace = "TestNameSpace",
    Params = new()
    {
        new(){ Kind = 0, Name = "class Player**" },
        new(){ Kind = 0, Name = "class CommandOrigin *&" },
        new(){ Kind = 0, Name = "long long&" },
        new(){ Kind = 0, Name = "struct Signature&&" }
    },
    RVA = 0,
    StorageClass = 0,
    SymbolType = 0,
    Symbol = "testMethodName@@+test@Sy@mbol@Stri@ng",
    Type = new() { Kind = 0, Name = "void** const&" }
}, false, autoGeneratingForProperty: false);

foreach (var line in methodData.Lines)
{
    Console.WriteLine(line);
}
