using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Microsoft.SourceBrowser.IO
{
    public class BinaryDeclaredSymbolInfoWriter : IDisposable
    {
        public BinaryDeclaredSymbolInfoWriter(Stream s, Common.Huffman huffman, bool leaveOpen = false)
        : this(new BinaryWriter(s, Encoding.Default, leaveOpen), huffman, false)
        {
        }

        public BinaryDeclaredSymbolInfoWriter(BinaryWriter writer, Common.Huffman huffman, bool leaveOpen = false)
        {
            Writer = writer;
            Huffman = huffman;
            LeaveOpen = leaveOpen;
        }

        private BinaryWriter Writer;
        private Common.Huffman Huffman;
        private bool LeaveOpen;

        public void Write(Common.Entity.DeclaredSymbolInfo symbol)
        {
            Write7BitEncodedInt(Writer, symbol.AssemblyNumber);
            Writer.Write(symbol.Name);
            Writer.Write(symbol.ID);
            WriteBytes(Writer, Huffman.Compress(symbol.Description));
            Write7BitEncodedInt(Writer, symbol.Glyph);
        }

        private static void WriteBytes(System.IO.BinaryWriter writer, byte[] array)
        {
            Write7BitEncodedInt(writer, array.Length);
            writer.Write(array);
        }

        private static void Write7BitEncodedInt(System.IO.BinaryWriter writer, int value)
        {
            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = (uint)value;   // support negative numbers
            while (v >= 0x80)
            {
                writer.Write((byte)(v | 0x80));
                v >>= 7;
            }

            writer.Write((byte)v);
        }

        public void Dispose()
        {
            if (!LeaveOpen)
                Writer.Dispose();
        }
    }
}
