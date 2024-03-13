using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Test")]

namespace Hosihikari.Generation;

internal interface IGenerator
{

    public void Initialize();

    public void Run();

    public void Save(string path);

}