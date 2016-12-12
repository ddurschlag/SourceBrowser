using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.SourceBrowser.IO
{
    public class Destination
    {
        public Destination(string fileName)
        : this(new string[0], fileName)
        {

        }
        public Destination(string[] folders, string fileName)
        {
            Folders = folders;
            FileName = fileName;
        }
        public string[] Folders { get; private set; }
        public string FileName { get; private set; }

        public override string ToString()
        {
            return System.IO.Path.Combine(Folders.Concat(new string[] { FileName }).ToArray());
        }

        public override bool Equals(object obj)
        {
            return
            (obj is Destination) &&
            ((Destination)obj).FileName == FileName &&
            ((Destination)obj).Folders.SequenceEqual(Folders);
        }

        public override int GetHashCode()
        {
            return AggregateHashCodes(FileName.GetHashCode(), Folders.Select(f => f.GetHashCode()));
        }

        private int AggregateHashCodes(int hash, IEnumerable<int> otherHashes)
        {
            return otherHashes.Aggregate(hash, (a, b) => Tuple.Create(a, b).GetHashCode());
        }
    }
}
