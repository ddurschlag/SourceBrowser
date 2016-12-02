using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Microsoft.SourceBrowser.IO
{
    public class SolutionManager : IOManager
    {
        private Dictionary<string, ProjectManager> _ProjectManagers = new Dictionary<string, ProjectManager>();
        private bool WriteDocumentsToDisk;

        public SolutionManager(string solutionDestinationFolder, bool writeDocumentsToDisk = true)
        {
            SolutionDestinationFolder = solutionDestinationFolder;
            WriteDocumentsToDisk = writeDocumentsToDisk;
        }

        public ProjectManager GetProjectManager(string assemblyId)
        {
            ProjectManager result;
            if (!_ProjectManagers.TryGetValue(assemblyId, out result))
            {
                result = new ProjectManager(this, GetProjectDestinationPath(assemblyId), assemblyId, WriteDocumentsToDisk);
            }
            return result;
        }

        public string SolutionDestinationFolder { get; private set; }

        //TODO: Should this return a destination?
        public string GetReferencedSymbolDestinationFilePath(string assemblyId, string symbolId)
        {
            return Path.Combine(
                SolutionDestinationFolder,
                assemblyId,
                Constants.PartialResolvingFileName,
                symbolId);
        }

        private string GetReferencesFilePath(string filename)
        {
            return Path.Combine(
                SolutionDestinationFolder,
                Constants.GuidAssembly,
                Constants.ReferencesFileName,
                filename
            );
        }

        public string GetReferencesRelativeHref(string filename, Destination d, ProjectManager pm)
        {
            return MakeRelativeToFile(
                GetReferencesFilePath(filename),
                pm.GetLegacyDocumentDestinationPath(d)
            ).Replace('\\', '/');
        }

        private string GetProjectDestinationPath(string assemblyId)
        {
            return Path.Combine(SolutionDestinationFolder, assemblyId);
        }
    }
}
