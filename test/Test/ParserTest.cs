using Hosihikari.Generation.CppParser;
using Hosihikari.Generation.Utils;
using System.Data;

namespace Test;

[TestClass]
public class ParserTest
{
    [TestMethod]
    [DataRow("enum class ExampleEnum", "ExampleEnum", CppTypeEnum.Enum)]
    [DataRow("class ExampleClass", "ExampleClass", CppTypeEnum.Class)]
    [DataRow("struct ExampleStruct", "ExampleStruct", CppTypeEnum.Struct)]
    [DataRow("enum ExampleEnum", "ExampleEnum", CppTypeEnum.Enum)]
    [DataRow("union ExampleUnion", "ExampleUnion", CppTypeEnum.Union)]
    public void TestTryGetCppType(string input, string output, CppTypeEnum type)
    {
        // Act
        bool rlt = CppTypeParser.TryGetCppType(input, out (CppTypeEnum type, string handledType) result);

        // Assert
        Assert.IsTrue(rlt);
        Assert.AreEqual(type, result.type);
        Assert.AreEqual(output, result.handledType);
    }

    private static IEnumerable<object?[]> TestTryGetTemplateTypeArgs()
        =>
        [["Template1<int>", true, "Template1", (string[])["int"]],
        ["Template2<bool, std::string>", true, "Template2", (string[])["bool", "std::string"]],
        ["Template3<std::pair<int, double>, int32_t>", true, "Template3", (string[])["std::pair<int, double>", "int32_t"]],
        ["Template4<int,int>", true, "Template4", (string[])["int", "int"]],
        ["Template5<   int ,    int >", true, "Template5", (string[])["int", "int"]],
        ["  Template6< int,    int >   ", true, "Template6", (string[])["int", "int"]],
        ["Template7", false, null, null]];

    [TestMethod]
    [DynamicData(nameof(TestTryGetTemplateTypeArgs), DynamicDataSourceType.Method)]
    public void TestTryGetTemplateType(string input, bool rlt, string output, string[] templateArgs)
    {
        // Act
        var success = CppTypeParser.TryGetTemplateType(input, out var result);

        // Assert
        Assert.AreEqual(rlt, success);
        Assert.AreEqual(output, result.typeWithoutTemplateArgs);

        if (success)
        {
            Assert.AreEqual(templateArgs.Length, result.templateArgs.Length);
            for (int i = 0; i < templateArgs.Length; i++)
            {
                Assert.AreEqual(templateArgs[i], result.templateArgs[i]);
            }
        }
        else
        {
            Assert.AreEqual(templateArgs, result.templateArgs);
        }
    }
}