namespace Hosihikari.Utils;

public enum SymbolType
{
    Constructor = 1,
    Destructor = 2,
    Operator = 3,
    StaticField = 4,
    Function = 5
}

public enum AccessType
{
    Public,
    Private,
    Protected,
    Empty
}
