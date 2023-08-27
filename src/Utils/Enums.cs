namespace Hosihikari.Utils;

public enum SymbolType
{
    Function = 0,
    Constructor = 1,
    Destructor,
    Operator,
    StaticField,
    UnknownVirtFunction = 5
}

public enum AccessType
{
    Public,
    Private,
    Protected,
    Empty
}
