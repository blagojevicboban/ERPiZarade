using System;
using System.Linq;
using DbfDataReader;
namespace FixHistory {
    class CheckColumns {
        public static void Run() {
            var opts = new DbfDataReaderOptions { SkipDeletedRecords = true };
            using var reader = new DbfDataReader.DbfDataReader(@"C:\PlataApp\Baze\RADNICII.DBF", opts);
            var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i).ToUpper().Trim()).ToList();
            Console.WriteLine("RADNICII columns:");
            Console.WriteLine(string.Join(", ", cols));
        }
    }
}
