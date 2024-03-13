using Hosihikari.Generation.CppParser;

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
    {
        return
        [
            ["Template1<int>", true, "Template1", (string[]) ["int"]],
            ["Template2<bool, std::string>", true, "Template2", (string[]) ["bool", "std::string"]],
            [
                "Template3<std::pair<int, double>, int32_t>", true, "Template3",
                (string[]) ["std::pair<int, double>", "int32_t"]
            ],
            ["Template4<int,int>", true, "Template4", (string[]) ["int", "int"]],
            ["Template5<   int ,    int >", true, "Template5", (string[]) ["int", "int"]],
            ["  Template6< int,    int >   ", true, "Template6", (string[]) ["int", "int"]],
            ["Template7", false, null, null]
        ];
    }

    [TestMethod]
    [DynamicData(nameof(TestTryGetTemplateTypeArgs), DynamicDataSourceType.Method)]
    public void TestTryGetTemplateType(string input, bool rlt, string output, string[] templateArgs)
    {
        // Act
        bool success =
            CppTypeParser.TryGetTemplateType(input, out (string[] templateArgs, string typeWithoutTemplateArgs) result);

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

    private static IEnumerable<object?[]> TestTryGetCppTypeNodesArgs()
    {
        return
        [
            [
                "class TestType", (CppType? type) =>
                {
                    return type is not null &&
                           (type.RootType == type) &&
                           type.Type is CppTypeEnum.Class &&
                           type.TypeIdentifier is "TestType" &&
                           type.TemplateTypes is null;
                }
            ],
            [
                "struct TestType  &&", (CppType? type) =>
                {
                    return type is not null &&
                           type.SubType is not null &&
                           type.SubType.Type is CppTypeEnum.RValueRef;
                }
            ],
            [
                "struct TestType[]", (CppType? type) =>
                {
                    return type is not null &&
                           type.SubType is not null &&
                           type.SubType.Type is CppTypeEnum.Array;
                }
            ],
            [
                "union TestType const  *  &", (CppType? type) =>
                {
                    return type is not null &&
                           type.Type is CppTypeEnum.Union &&
                           type.TypeIdentifier is "TestType" &&
                           type.TemplateTypes is null &&
                           type.SubType is not null &&
                           type.SubType.Type is CppTypeEnum.Pointer &&
                           type.SubType.IsConst &&
                           type.SubType.SubType is not null &&
                           type.SubType.SubType.Type is CppTypeEnum.Ref;
                }
            ],
            [
                "class test::details::TestType<std::tuple<std::string>> const&", (CppType? type) =>
                {
                    return type is not null &&
                           type.Type is CppTypeEnum.Class &&
                           type.TypeIdentifier is "test::details::TestType" &&
                           type.SubType is not null &&
                           type.SubType.Type is CppTypeEnum.Ref &&
                           type.SubType.IsConst &&
                           type.TemplateTypes is not null &&
                           type.TemplateTypes.Length is 1 &&
                           type.TemplateTypes[0].Type is CppTypeEnum.Class &&
                           type.TemplateTypes[0].TypeIdentifier is "std::tuple" &&
                           type.TemplateTypes[0].TemplateTypes is not null &&
                           type.TemplateTypes[0].TemplateTypes!.Length is 1 &&
                           type.TemplateTypes[0].TemplateTypes![0].Type is CppTypeEnum.Class &&
                           type.TemplateTypes[0].TemplateTypes![0].TypeIdentifier is "std::string";
                }
            ],
            ["", (CppType? type) => false],
            ["TestType[&*]", (CppType? type) => false]
        ];
    }

    [TestMethod]
    [DynamicData(nameof(TestTryGetCppTypeNodesArgs), DynamicDataSourceType.Method)]
    public void TestTryParseCppTypeNodes(string input, Func<CppType?, bool> func)
    {
        // Act
        bool success = CppTypeParser.TryParseCppTypeNodes(input, out CppType? result);

        // Assert
        bool rlt = func(result);
        Assert.AreEqual(success, rlt);
    }
}