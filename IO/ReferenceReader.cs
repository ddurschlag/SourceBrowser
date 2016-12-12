using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Microsoft.SourceBrowser.IO
{
    public class ReferenceReader : IDisposable
    {
        public ReferenceReader(StreamReader reader, bool leaveOpen = false)
        {
            Reader = reader;
            LeaveOpen = leaveOpen;
        }

        private StreamReader Reader;
        private bool LeaveOpen;

        public Common.Entity.Reference Read()
        {
            var l1 = Reader.ReadLine();
            var l2 = Reader.ReadLine();
            if (l1 != null && l2 != null)
                return new Common.Entity.Reference(l1, l2);
            return null;
        }

        public void Dispose()
        {
            if (!LeaveOpen)
            {
                Reader.Dispose();
            }
        }
    }
}
