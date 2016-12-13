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
        private string InputPath { get; set; }
//        private SolutionManager IOManager { get; set; }

        public SymbolReferencesThingy(string inputPath)//, SolutionManager ioManager)
        {
            InputPath = inputPath;
  //          IOManager = ioManager;
        }

        public IEnumerable<Common.Entity.Reference> ReadAllReferences()
        {
            using (var rr = new ReferenceReader(new StreamReader(InputPath)))
            {
                var r = rr.Read();
                while (r != null)
                {
                    yield return r;
                    r = rr.Read();
                }
            }
        }

        //public StreamWriter GetOutputWriter(Common.Entity.Reference r)
        //{
        //    return IOManager.GetProjectManager(r.ToAssemblyId).GetReferenceHtmlWriter(SymbolId);
        //}

        public string SymbolId { get { return Path.GetFileNameWithoutExtension(InputPath); } }
    }
}
