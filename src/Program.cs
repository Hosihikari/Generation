using Hosihikari.Generation;
using System.Diagnostics;

Stopwatch watcher = new();
Console.WriteLine($"Start generating at {DateTime.Now}");
watcher.Start();
Main.Run(new("originalData.json", "out"));
watcher.Stop();
Console.WriteLine($"Generated successfully at {DateTime.Now}, which took {watcher.Elapsed}");