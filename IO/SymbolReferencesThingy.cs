using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Microsoft.SourceBrowser.IO
{
    /// <summary>
    /// todo: Make this store all its explicitly, rather than half of it being implied by the location of serialized data
    /// </summary>
    public class SymbolReferencesThingy
    {
        private string FilePath { get; set; }

        public SymbolReferencesThingy(string inputPath)
        {
            FilePath = inputPath;
        }

        public IEnumerable<Common.Entity.Reference> ReadAllReferences()
        {
            using (var rr = new ReferenceReader(new StreamReader(FilePath)))
            {
                var r = rr.Read();
                while (r != null)
                {
                    yield return r;
                    r = rr.Read();
                }
            }
        }

        public StreamWriter GetOutputWriter()
        {
            return new StreamWriter(Path.ChangeExtension(FilePath, ".html"), false, Encoding.UTF8);
        }

        public string SymbolId { get { return Path.GetFileNameWithoutExtension(FilePath); } }
    }
}
