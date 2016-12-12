using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Microsoft.SourceBrowser.IO
{
    public class ReferenceWriter : IDisposable
    {
        public ReferenceWriter(StreamWriter writer, bool leaveOpen = false)
        {
            Writer = writer;
            LeaveOpen = leaveOpen;
        }

        private StreamWriter Writer;
        private bool LeaveOpen;

        public void Write(Common.Entity.Reference r)
        {
            Writer.Write(r.FromAssemblyId);
            Writer.Write(';');
            Writer.Write(r.Url);
            Writer.Write(';');
            Writer.Write(r.FromLocalPath);
            Writer.Write(';');
            Writer.Write(r.ReferenceLineNumber);
            Writer.Write(';');
            Writer.Write(r.ReferenceColumnStart);
            Writer.Write(';');
            Writer.Write(r.ReferenceColumnEnd);
            Writer.Write(';');
            Writer.Write((int)r.Kind);
            Writer.WriteLine();
            Writer.WriteLine(r.ReferenceLineText);
        }

        public void Dispose()
        {
            if (!LeaveOpen)
                Writer.Dispose();
        }
    }
}
