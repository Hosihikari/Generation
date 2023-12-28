namespace Hosihikari.Generation.Generator;

public static class Utils
{
    public static string StrIfTrue(string str, bool b)
    {
        return b ? str : string.Empty;
    }
}