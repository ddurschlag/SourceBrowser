using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.SourceBrowser.Common
{
    public class SymbolLocation
    {
        public static SymbolLocation Read(string line)
        {
            var parts = line.Split(';');
            var streamOffset = long.Parse(parts[1]);
            return new SymbolLocation(parts[0], streamOffset);
        }

        public static void Write(System.IO.StreamWriter sw, SymbolLocation location)
        {
            sw.Write(location.FilePath);
            sw.Write(";");
            sw.WriteLine(location.Offset);
        }

        public SymbolLocation(string filePath, long offset)
        {
            FilePath = filePath;
            Offset = offset;
        }

        public string FilePath { get; private set; }
        public long Offset { get; private set; }
        public override int GetHashCode()
        {
            return Tuple.Create(FilePath, Offset).GetHashCode();
        }
        public override bool Equals(object obj)
        {
            return obj is SymbolLocation && (
                ((SymbolLocation)obj).FilePath == FilePath &&
                ((SymbolLocation)obj).Offset == Offset
            );
        }
        public override string ToString()
        {
            return string.Format("{0};{1}", FilePath, Offset);
        }
    }
}
