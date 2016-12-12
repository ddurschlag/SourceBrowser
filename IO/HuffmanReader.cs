using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Microsoft.SourceBrowser.IO
{
    //todo: Does this need a binary reader? It only seems to read bytes, and the only
    //difference is that binaryreaders throw instead of providing -1.
    public class HuffmanReader : IDisposable
    {
        public HuffmanReader(Stream s, bool leaveOpen = false)
        : this(new BinaryReader(s, Encoding.Default, leaveOpen), false)
        { }

        public HuffmanReader(BinaryReader reader, bool leaveOpen = false)
        {
            Reader = reader;
            LeaveOpen = leaveOpen;
        }

        private BinaryReader Reader;
        private bool LeaveOpen;

        public Common.Huffman Read()
        {
            return new Common.Huffman(ReadNode());
        }

        private Common.Huffman.Node ReadNode()
        {
            var stack = new Stack<Common.Huffman.Node>();
            while (true)
            {
                int readByte = Reader.BaseStream.ReadByte();
                if (readByte == -1)
                {
                    return stack.Pop();
                }

                byte b = (byte)readByte;
                if (b == byte.MaxValue)
                {
                    if (stack.Count > 1)
                    {
                        var right = stack.Pop();
                        var left = stack.Pop();
                        Common.Huffman.Node newNode = new Common.Huffman.Node(left, right);
                        stack.Push(newNode);
                    }
                    else
                    {
                        return stack.Pop();
                    }
                }
                else
                {
                    byte to = Reader.ReadByte();
                    Common.Huffman.Node newNode = new Common.Huffman.Node(0, b, to);
                    stack.Push(newNode);
                }
            }
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
