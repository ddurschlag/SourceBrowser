using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.SourceBrowser.IO
{
    public class Destination
    {
        public Destination(string[] folders, string fileName)
        {
            Folders = folders;
            FileName = fileName;
        }
        public string[] Folders { get; private set; }
        public string FileName { get; private set; }

        public override string ToString()
        {
            return System.IO.Path.Combine(Folders.Concat(new string[] { FileName }).ToArray());
        }
    }
}
