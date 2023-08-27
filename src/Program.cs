using Hosihikari.Generation;

var type = TypeAnalyzer.Analyze("union QAQ");
Console.WriteLine(type.CppTypeHandle.ToString());
