using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hosihikari.Generation.Generator;

public static class Utils
{
    public static string StrIfTrue(string str, bool b) => b ? str : string.Empty;
    public static string StrIfFalse(string str, bool b) => b ? string.Empty : str;
}
