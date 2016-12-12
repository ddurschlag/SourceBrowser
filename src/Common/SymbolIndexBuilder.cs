using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.SourceBrowser.Common
{
    public class SymbolIndexBuilder
    {
        private SymbolIndex Current;
        private string SymbolId;

        private void Reset()
        {
            Current = new SymbolIndex();
            SymbolId = null;
        }

        public SymbolIndexBuilder()
        {
            Reset();
        }

        public SymbolIndex Build()
        {
            var result = Current;
            Reset();
            return result;
        }

        public void Process(string line)
        {
            if (line.StartsWith("="))
            {
                SymbolId = line.Substring(1);
            }
            else if (!string.IsNullOrWhiteSpace(line) && SymbolId != null)
            {
                Current.Add(SymbolId, SymbolLocation.Read(line));
            }
        }
    }
}
