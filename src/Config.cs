namespace Hosihikari.Generation;

public class Config(string originalDataPath, string assemblyOutputDir)
{
    public readonly string AssemblyOutputDir = assemblyOutputDir;
    public readonly string OriginalDataPath = originalDataPath;
}