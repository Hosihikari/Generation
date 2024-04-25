using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Test")]

namespace Hosihikari.Generation;

internal interface IGenerator
{
    public void Initialize();

    public ValueTask RunAsync();

    public ValueTask SaveAsync(string path);
}