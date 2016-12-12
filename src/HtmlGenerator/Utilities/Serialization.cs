using System;
using System.Collections.Generic;
using Path = System.IO.Path;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.SourceBrowser.Common;
using Microsoft.SourceBrowser.Common.Entity;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class Serialization
    {
        public static string GetIconForExtension(string document)
        {
            switch (Path.GetExtension(document).ToLowerInvariant())
            {
                case ".cs":
                    return "196";
                case ".vb":
                    return "195";
                case ".ts":
                    return "228";
                default:
                    return "227";
            }
        }

        public static string ByteArrayToHexString(byte[] bytes, int digits = 0)
        {
            if (digits == 0)
            {
                digits = bytes.Length * 2;
            }

            char[] c = new char[digits];
            byte b;
            for (int i = 0; i < digits / 2; i++)
            {
                b = ((byte)(bytes[i] >> 4));
                c[i * 2] = (char)(b > 9 ? b + 87 : b + 0x30);
                b = ((byte)(bytes[i] & 0xF));
                c[i * 2 + 1] = (char)(b > 9 ? b + 87 : b + 0x30);
            }

            return new string(c);
        }
        
        public static string ReadValue(IEnumerable<string> lines, string name)
        {
            name += "=";
            var line = lines.FirstOrDefault(l => l.StartsWith(name));
            if (line == null)
            {
                return string.Empty;
            }

            return line.Substring(name.Length);
        }

        public static long ReadLong(IEnumerable<string> lines, string name)
        {
            string value = ReadValue(lines, name);
            long result = 0;
            long.TryParse(value, out result);
            return result;
        }

        public static byte[] ReadNativeBytes(IntPtr pointer, int count)
        {
            byte[] result = new byte[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = Marshal.ReadByte(pointer, i);
            }

            return result;
        }
    }
}
