using System.Diagnostics;
using Hosihikari.Generation;

Stopwatch watcher = new();
Console.WriteLine($"Start generating at {DateTime.Now}");
watcher.Start();
Main.Run(new("originalData.json", "out"));
watcher.Stop();
Console.WriteLine($"Generated successfully at {DateTime.Now}, which took {watcher.Elapsed}");