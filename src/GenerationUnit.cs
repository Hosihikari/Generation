using Hosihikari.Generation.Generator;
using Hosihikari.Utils;
using System.Runtime.InteropServices.Marshalling;
using static Hosihikari.Utils.OriginalData;

namespace Hosihikari.Generation;

//public class GenerationUnit
//{
//    static GenerationUnit()
//    {
//        foreach (var (className, @class) in GlobalData.Classes)
//        {
//            var type = new TypeData(new() { Kind = 0, Name = className });
//            if (Types.ContainsKey(type.FullTypeIdentifier) is false)
//            {
//                Types.Add(type.FullTypeIdentifier, new(type.FullTypeIdentifier));
//            }

//            var arr = new List<Class.Item>[]
//            {
//                @class.Public,
//                @class.Virtual,
//                @class.Protected,
//                @class.PublicStatic,
//                @class.PrivateStatic,
//            };

//            foreach (var list in arr)
//            {
//                if (list is null) continue;

//                foreach (var item in list)
//                {

//                    if (item.Params is not null)
//                        foreach (var param in item.Params)
//                        {
//                            try
//                            {
//                                var paramType = new TypeData(param);
//                                if (Types.ContainsKey(paramType.FullTypeIdentifier) is false)
//                                {
//                                    Types.Add(paramType.FullTypeIdentifier, new(paramType.FullTypeIdentifier));
//                                }
//                            }
//                            catch (Exception) { continue; }
//                        }

//                    if (string.IsNullOrWhiteSpace(item.Type.Name) is false)
//                    {
//                        try
//                        {
//                            var resultType = new TypeData(item.Type);
//                            if (Types.ContainsKey(resultType.FullTypeIdentifier) is false)
//                            {
//                                Types.Add(resultType.FullTypeIdentifier, new(resultType.FullTypeIdentifier));
//                            }
//                        }
//                        catch (Exception) { continue; }
//                    }
//                }
//            }
//        }
//    }

//    private static readonly Dictionary<string, GenerationUnit> Types = new();

//    public string Name { get; private set; }
//    public Class? Class { get; private set; }
//    public (TypeData data, string NamespaceString, string[] Namespaces) Type { get; private set; }

//    public List<ConstructorData> Constructors { get; private set; } = new();
//    public List<MethodData> Methods { get; private set; } = new();
//    public List<VirtualMethodData> VirtualMethods { get; private set; } = new();

//    private GenerationUnit(string name)
//    {
//        Name = name;
//        var type = new TypeData(new()
//        {
//            Kind = 0,
//            Name = name
//        });
//        var namespaces = type.Analyzer.CppTypeHandle.Namespaces;
//        Type = (type, namespaces is null ? string.Empty : string.Join('.', namespaces), namespaces is null ? Array.Empty<string>() : namespaces);
//    }

//    //public static GenerationUnit SetClassData(string className, in Class @class)
//    //{
//    //    var type = new TypeData(new() { Kind = 0, Name = className });
//    //    if (Types.TryGetValue(type.FullTypeIdentifier, out var unit))
//    //    {
//    //        unit.Class = @class;
//    //    }
//    //}



//    //private GenerationUnit(string className, in Class @class)
//    //{
//    //    Name = className;
//    //    Class = @class;
//    //    var type = new TypeData(new()
//    //    {
//    //        Kind = 0,
//    //        Name = className
//    //    });
//    //    var namespaces = type.Analyzer.CppTypeHandle.Namespaces;
//    //    Type = (type, namespaces is null ? string.Empty : string.Join('.', namespaces), namespaces is null ? Array.Empty<string>() : namespaces);

//    //    if (Class.Public is not null)
//    //    {
//    //        foreach (var item in Class.Public)
//    //        {
//    //            try
//    //            {
//    //                switch ((SymbolType)item.SymbolType)
//    //                {
//    //                    case SymbolType.Function:
//    //                        Methods.Add(new(item, false));
//    //                        break;
//    //                    case SymbolType.Constructor:
//    //                        Constructors.Add(new(item, Name));
//    //                        break;

//    //                    case SymbolType.Destructor:
//    //                    case SymbolType.Operator:
//    //                    case SymbolType.StaticField:
//    //                    case SymbolType.UnknownVirtFunction:
//    //                        break;
//    //                }
//    //            }
//    //            catch (Exception)
//    //            {
//    //                continue;
//    //            }
//    //        }

//    //        foreach (var item in Class.PublicStatic)
//    //        {
//    //            try
//    //            {
//    //                switch ((SymbolType)item.SymbolType)
//    //                {
//    //                    case SymbolType.Function:
//    //                        Methods.Add(new(item, true));
//    //                        break;
//    //                    case SymbolType.Constructor:
//    //                        Constructors.Add(new(item, Name));
//    //                        break;

//    //                    case SymbolType.Destructor:
//    //                    case SymbolType.Operator:
//    //                    case SymbolType.StaticField:
//    //                    case SymbolType.UnknownVirtFunction:
//    //                        break;
//    //                }
//    //            }
//    //            catch (Exception)
//    //            {
//    //                continue;
//    //            }
//    //        }

//    //        ulong index = 0;
//    //        foreach (var item in Class.Virtual)
//    //        {
//    //            try
//    //            {
//    //                switch ((SymbolType)item.SymbolType)
//    //                {
//    //                    case SymbolType.Function:
//    //                        VirtualMethods.Add(new(item, index));
//    //                        break;
//    //                    case SymbolType.Constructor:
//    //                    case SymbolType.Destructor:
//    //                    case SymbolType.Operator:
//    //                    case SymbolType.StaticField:
//    //                    case SymbolType.UnknownVirtFunction:
//    //                        break;
//    //                }
//    //            }
//    //            catch (Exception)
//    //            {
//    //                continue;
//    //            }
//    //            index++;
//    //        }
//    //    }
//    //}

//    public void WriteAllLines()
//    {

//    }
//}
