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
                return Parse(l1, l2);
            return null;
        }

        private Common.Entity.Reference Parse(string separatedLine, string sourceLine)
        {
            Common.Entity.ReferenceKind kind = default(Common.Entity.ReferenceKind);

            var parts = separatedLine.Split(';');
            if (parts.Length >= 8)
            {
                kind = (Common.Entity.ReferenceKind)int.Parse(parts[7]);
            }

            return new Common.Entity.Reference(
                string.Intern(parts[0]),
                parts[1],
                parts[2],
                parts[3],
                int.Parse(parts[4]),
                int.Parse(parts[5]),
                int.Parse(parts[6]),
                sourceLine,
                kind
            );
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
