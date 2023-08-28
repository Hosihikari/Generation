using Hosihikari.Generation;
using Hosihikari.Generation.Generator;

var data = new TypeData(new() { Kind = 0, Name = "class TestClass**&&" });
Console.WriteLine(data.Type);
