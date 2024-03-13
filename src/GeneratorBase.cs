using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Test")]

namespace Hosihikari.Generation;

internal abstract class GeneratorBase
{
    public GeneratorBase(string originalFilePath)
    {
        _ = originalFilePath;
    }

    public abstract void Initialize();

    public abstract void Run();

    public abstract void Save(string path);
}