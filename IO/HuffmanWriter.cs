using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.IO
{
    public class HuffmanWriter : Common.Huffman.Visitor, IDisposable
    {
        public HuffmanWriter(Stream s, bool leaveOpen = false)
        : this(new BinaryWriter(s, Encoding.Default, leaveOpen), false)
        {
        }
        public HuffmanWriter(BinaryWriter writer, bool leaveOpen = false)
        {
            Writer = writer;
            LeaveOpen = leaveOpen;
        }

        private BinaryWriter Writer;
        private bool LeaveOpen;

        public void Write(Common.Huffman huffman)
        {
            Visit(huffman.root);
        }

        public override void VisitLeaf(Huffman.Node n)
        {
            Writer.Write(n.from);
            Writer.Write(n.to);
        }

        public override void VisitBranch(Huffman.Node n)
        {
            Visit(n.left);
            Visit(n.right);
            Writer.Write(byte.MaxValue);
        }
        public void Dispose()
        {
            if (!LeaveOpen)
                Writer.Dispose();
        }
    }
}
