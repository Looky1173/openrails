using Orts.Formats;

var consist1 = new ConsistFile("D:\\Users\\Gergely\\OR\\Content\\BNSF Starter Route\\TRAINS\\consists\\Everett Switcher.con");
var consist2 = new ConsistFile("D:\\Users\\Gergely\\OR\\Content\\BNSF Starter Route\\TRAINS\\consists\\consist.or-consist.toml");
Console.WriteLine(ObjectDumper.Dump(consist1));
Console.WriteLine(ObjectDumper.Dump(consist2));
