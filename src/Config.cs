using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hosihikari.Generation;

public class Config
{
    public string OriginalDataPath;
    public string AssemblyOutputDir;

    public Config(string originalDataPath, string assemblyOutputDir)
    {
        OriginalDataPath = originalDataPath;
        AssemblyOutputDir = assemblyOutputDir;
    }
}
