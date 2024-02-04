namespace Hosihikari.Generation;

public class Config(string originalDataPath, string assemblyOutputDir, Version assemblyversion)
{
    public readonly string AssemblyOutputDir = assemblyOutputDir;
    public readonly Version AssemblyVersion = assemblyversion;
    public readonly string OriginalDataPath = originalDataPath;
}