namespace Hosihikari.Generation.CppParser;

public enum CppTypeEnum
{
    FundamentalType,
    Pointer,
    Ref,
    RValueRef,
    Enum,
    Class,
    Struct,
    Union,
    Array,
    VarArgs,

    Function // not implemented
}