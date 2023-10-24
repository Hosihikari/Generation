namespace Hosihikari.Utils;

public enum SymbolType
{
    Function = 0,
    Constructor = 1,
    Destructor = 2,
    Operator = 3,
    StaticField = 4,
    UnknownFunction = 5
}

public enum AccessType
{
    Public,
    Private,
    Protected,
    Empty
}
