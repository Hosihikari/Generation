namespace Hosihikari.Generation;

internal interface IGenerator
{
    void Initialize();
    void Run();
    void Save(string path);
}