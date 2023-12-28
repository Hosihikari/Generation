using Hosihikari.Generation;

//var temp = new TypeData(new() { Kind = 0, Name = "class pd::qaq::test::TestClass**&" });
//var temp1 = new TypeData(new() { Kind = 0, Name = "class pd::qaq::test::TestClass&" });
//var temp2 = new TypeData(new() { Kind = 0, Name = "class pd::qaq::test::TestClass* const&" });

//var methodData = new MethodData(new Hosihikari.Utils.OriginalData.Class.Item()
//{
//    AccessType = (int)AccessType.Public,
//    FakeSymbol = string.Empty,
//    FlagBits = 0,
//    Name = "testMethodName",
//    Namespace = "TestNameSpace",
//    Params = new()
//    {
//        new(){ Kind = 0, Name = "class Player**" },
//        new(){ Kind = 0, Name = "class CommandOrigin *&" },
//        new(){ Kind = 0, Name = "long long&" },
//        new(){ Kind = 0, Name = "struct Signature&&" }
//    },
//    RVA = 0,
//    StorageClass = 0,
//    SymbolType = 0,
//    Symbol = "testMethodName@@+test@Sy@mbol@Stri@ng",
//    Type = new() { Kind = 0, Name = "void** const&" }
//}, false, autoGeneratingForProperty: false);

//foreach (var line in methodData.Lines)
//{
//    Console.WriteLine(line);
//}

Main.Run(new("""C:\Users\minec\Desktop\originalData.json""", """C:\Users\minec\Desktop"""));