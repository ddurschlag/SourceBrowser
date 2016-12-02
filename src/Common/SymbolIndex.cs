using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.SourceBrowser.Common
{
    public class SymbolIndex : IEnumerable<Tuple<string, IEnumerable<SymbolLocation>>>
    {
        private SortedDictionary<string, HashSet<SymbolLocation>> index = new SortedDictionary<string, HashSet<SymbolLocation>>();

        private HashSet<SymbolLocation> Get(string symbolID)
        {
            HashSet<SymbolLocation> result;
            if (!index.TryGetValue(symbolID, out result))
            {
                result = new HashSet<SymbolLocation>();
                index.Add(symbolID, result);
            }

            return result;
        }

        public void Add(string symbolID, SymbolLocation location)
        {
            Get(symbolID).Add(location);
        }

        public void Add(string symbolID, string filePath, long offset)
        {
            Add(symbolID, new SymbolLocation(filePath, offset));
        }

        public IEnumerator<Tuple<string, IEnumerable<SymbolLocation>>> GetEnumerator()
        {
            foreach (var kvp in index)
            {
                yield return Tuple.Create(kvp.Key, (IEnumerable<SymbolLocation>)kvp.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

}
