using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Microsoft.SourceBrowser.IO
{
    public class ProjectManager : IOManager
    {
        internal ProjectManager(SolutionManager parent, string projectDestinationFolder, string assemblyId, bool writeDocumentsToDisk)
        {
            Parent = parent;
            ProjectDestinationFolder = projectDestinationFolder;
            AssemblyId = assemblyId;
            WriteDocumentsToDisk = writeDocumentsToDisk;
        }
        public string ProjectDestinationFolder { get; private set; }
        public bool WriteDocumentsToDisk { get; private set; }
        public SolutionManager Parent { get; private set; }
        public string AssemblyId { get; private set; }

        public bool DestinationExists(Destination d)
        {
            return File.Exists(GetDestinationPath(d));
        }

        public void CreateDirectory(Destination d)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GetDestinationPath(d)));
        }

        public StreamWriter GetWriter(Destination d)
        {
            if (WriteDocumentsToDisk)
                return new StreamWriter(
                    GetDestinationPath(d),
                    append: false,
                    encoding: Encoding.UTF8
                );
            else
            {
                return new StreamWriter(new MemoryStream());
            }
        }

        public string GetUrlFromDestinationToSymbol(Destination d, string symbolId)
        {
            var referencedSymbolDestinationFilePath = GetReferencedSymbolDestinationFilePath(symbolId);
            return GetUrlFromDestinationToPath(d, symbolId, referencedSymbolDestinationFilePath);
        }

        public string GetUrlFromDestinationToPath(Destination d, string symbolId, string path)
        {
            var destinationPath = GetDestinationPath(d);
            string href;

            if (path + ".html" == destinationPath)
            {
                href = "";
            }
            else
            {
                href = (MakeRelativeToFile(path, destinationPath) + ".html").Replace('\\', '/');
            }
            return href + "#" + symbolId;
        }

        public string GetUrlFromDestinationToRelativePath(Destination d, string symbolId, string relativePath)
        {
            return GetUrlFromDestinationToPath(d, symbolId, Path.Combine(ProjectDestinationFolder, relativePath));
        }

        private string GetReferencedSymbolDestinationFilePath(string symbolId)
        {
            return Parent.GetReferencedSymbolDestinationFilePath(this.AssemblyId, symbolId);
        }

        public string GetPathToSolutionRoot(Destination d)
        {
            var relativePath = GetDestinationPath(d).Substring(Parent.SolutionDestinationFolder.Length + 1);
            var depth = relativePath.Count(c => c == '\\' || c == '/');

            var sb = new StringBuilder(3 * depth);
            for (int i = 0; i < depth; i++)
            {
                sb.Append("../");
            }
            return sb.ToString();
        }

        public string GetSolutionRelativeReferenceHref(string filename, Destination d)
        {
            return Parent.GetReferencesRelativeHref(filename, d, this);
        }



        [Obsolete("Retire in favor of using destinations")]
        public string GetLegacyDocumentDestinationPath(Destination d)
        {
            return GetDestinationPath(d);
        }

        private string GetDestinationPath(Destination d)
        {
            return Path.GetFullPath(Path.Combine(
                new string[] { ProjectDestinationFolder }
                .Concat(d.Folders)
                .Concat(new string[] { d.FileName })
                .ToArray()
            ) + ".html");
        }

        public string GetRelativeReferenceHref(string filename, Destination d)
        {
            return GetRelativeHref(filename, d, Constants.ReferencesFileName);
        }

        public string GetRelativePartialFileHref(string filename, Destination d)
        {
            return GetRelativeHref(filename, d, Constants.PartialResolvingFileName);
        }

        private string GetRelativeHref(string filename, Destination d, params string[] subdir)
        {
            return MakeRelativeToFile(
                Path.Combine(new string[] { ProjectDestinationFolder }.Concat(subdir).Concat(new string[] { filename }).ToArray()),
                GetDestinationPath(d)
            ).Replace('\\', '/');
        }

        private string GetReferencesFilePath(string filename)
        {
            return Path.Combine(ProjectDestinationFolder, Constants.ReferencesFileName, filename);
        }

    }
}
