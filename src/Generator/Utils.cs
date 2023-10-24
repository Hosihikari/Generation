namespace Hosihikari.Generation.Generator;

public static class Utils
{
    public static string StrIfTrue(string str, bool b) => b ? str : string.Empty;
    public static string StrIfFalse(string str, bool b) => b ? string.Empty : str;

    static ulong Hash(string str)
    {
        ulong rval = 0;
        for (int i = 0; i < str.Length; ++i)
        {
            if ((i & 1) > 0)
            {
                rval ^= (~((rval << 11) ^ str[i] ^ (rval >> 5)));
            }
            else
            {
                rval ^= (~((rval << 7) ^ str[i] ^ (rval >> 3)));
            }
        }
        return rval;
    }
}
