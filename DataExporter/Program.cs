using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cyalume = Lumina.Lumina;

namespace DataExporter
{
    class Program
    {
        static void Main(string[] args)
        {
            var sqpack = args.Length == 0 ? @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack" : args[0];
            var cyalume = new Cyalume(sqpack);
            var worlds = new Dictionary<int, string>(
                cyalume.GetExcelSheet<World>()
                    .GetRows()
                    .Where(row => row.IsPublic)
                    .Select(row => new KeyValuePair<int, string>(row.RowId, row.Name)));
            File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "world.json"), JsonConvert.SerializeObject(worlds));
        }
    }
}
