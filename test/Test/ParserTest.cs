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
                "class TestType", (CppType? type) => type is
                {
                    Type: CppTypeEnum.Class,
                    TypeIdentifier: "TestType",
                    TemplateTypes: null
                } && (type.RootType == type)
            ],
            [
                "struct TestType  &&", (CppType? type) => type is
                {
                    SubType.Type: CppTypeEnum.RValueRef
                }
            ],
            [
                "struct TestType[]", (CppType? type) => type is
                {
                    SubType.Type: CppTypeEnum.Array
                }
            ],
            [
                "union TestType const  *  &", (CppType? type) => type is
                {
                    Type: CppTypeEnum.Union,
                    TypeIdentifier: "TestType",
                    TemplateTypes: null,
                    SubType:
                    {
                        Type: CppTypeEnum.Pointer,
                        IsConst: true,
                        SubType.Type: CppTypeEnum.Ref
                    }
                }
            ],
            [
                "class test::details::TestType<std::tuple<std::string>> const&", (CppType? type) => type is
                {
                    Type: CppTypeEnum.Class,
                    TypeIdentifier: "test::details::TestType",
                    SubType:
                    {
                        Type: CppTypeEnum.Ref,
                        IsConst: true
                    },
                    TemplateTypes:
                    [
                        {
                            Type: CppTypeEnum.Class,
                            TypeIdentifier: "std::tuple",
                            TemplateTypes:
                            [
                                {
                                    Type: CppTypeEnum.Class,
                                    TypeIdentifier: "std::string"
                                }
                            ]
                        }
                    ]
                }
            ],
            [
                "class test::TestType<class std::pair<int, double* const>>", (CppType? type) => type is
                {
                    Type: CppTypeEnum.Class,
                    TypeIdentifier: "test::TestType",
                    TemplateTypes:
                    [
                        {
                            Type: CppTypeEnum.Class,
                            TypeIdentifier: "std::pair",
                            TemplateTypes:
                            [
                                {
                                    Type: CppTypeEnum.FundamentalType,
                                    TypeIdentifier: "int",
                                    FundamentalType: CppFundamentalType.Int32
                                },
                                {
                                    Type: CppTypeEnum.FundamentalType,
                                    TypeIdentifier: "double",
                                    FundamentalType: CppFundamentalType.Double,
                                    SubType:
                                    {
                                        Type: CppTypeEnum.Pointer,
                                        IsConst: true
                                    }
                                }
                            ]
                        }
                    ]
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

    [TestMethod]
    [DataRow("int", true, CppFundamentalType.Int32)]
    [DataRow("float", true, CppFundamentalType.Float)]
    [DataRow("char", true, CppFundamentalType.Char)]
    [DataRow("signed char", true, CppFundamentalType.SChar)]
    [DataRow("wchar_t", true, CppFundamentalType.WChar)]
    [DataRow("unsigned __int64", true, CppFundamentalType.UInt64)]
    public void TestTryGetFundamentalType(string input, bool val, CppFundamentalType? type)
    {
        bool result = CppTypeParser.TryGetFundamentalType(input, out CppFundamentalType? fundamentalType);

        Assert.AreEqual(result, val);
        Assert.AreEqual(type, fundamentalType);
    }
}