using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Microsoft.SourceBrowser.IO
{
    public class TextDeclaredSymbolInfoWriter : IDisposable
    {
        public TextDeclaredSymbolInfoWriter(StreamWriter writer, bool leaveOpen = false)
        {
            Writer = writer;
            LeaveOpen = leaveOpen;
        }

        public void Write(Common.Entity.DeclaredSymbolInfo symbol)
        {
            Writer.WriteLine(
                @"{0};{1};{2};{3};{4}",
                symbol.Name,
                Common.TextUtilities.ULongToHexString(symbol.ID),
                symbol.Kind,
                symbol.Description,
                symbol.Glyph
            );
        }

        private StreamWriter Writer;
        private bool LeaveOpen;

        public void Dispose()
        {
            if (!LeaveOpen)
                Writer.Dispose();
        }

    }
}
