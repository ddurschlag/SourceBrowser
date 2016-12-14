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
        private object _ProjectManagersLock = new object();
        private bool WriteDocumentsToDisk;
        private bool CreateFoldersOnDisk;
        private int Parallelism;

        public SolutionManager(string solutionDestinationFolder, bool writeDocumentsToDisk = true, bool createFoldersOnDisk = true, int parallelism = -1)
        {
            SolutionDestinationFolder = solutionDestinationFolder;
            WriteDocumentsToDisk = writeDocumentsToDisk;
            CreateFoldersOnDisk = createFoldersOnDisk;
            Parallelism = (parallelism == -1) ? System.Environment.ProcessorCount : parallelism;
        }

        public IEnumerable<ProjectManager> ProjectManagers
        {
            get { return _ProjectManagers.Values; }
        }

        public void EnsureProjectManager(string assemblyId)
        {
            if (!_ProjectManagers.ContainsKey(assemblyId))
            {
                lock (_ProjectManagersLock)
                {
                    if (!_ProjectManagers.ContainsKey(assemblyId))
                    {
                        _ProjectManagers.Add(assemblyId, ManufactureProjectManager(assemblyId));
                    }
                }
            }
        }

        private ProjectManager ManufactureProjectManager(string assemblyId)
        {
            var result = new ProjectManager(this, GetProjectDestinationPath(assemblyId), assemblyId, WriteDocumentsToDisk, Parallelism);
            if (CreateFoldersOnDisk)
                result.CreateDirectory();
            return result;
        }

        public ProjectManager GetProjectManager(string assemblyId)
        {
            Common.Log.Write("Retrieving project manager " + assemblyId, ConsoleColor.Cyan);
            ProjectManager result;
            if (!_ProjectManagers.TryGetValue(assemblyId, out result))
            {
                lock (_ProjectManagersLock)
                {
                    if (!_ProjectManagers.TryGetValue(assemblyId, out result))
                    {
                        result = ManufactureProjectManager(assemblyId);
                        _ProjectManagers.Add(assemblyId, result);
                    }
                }
            }
            return result;
        }

        private Dictionary<string, Dictionary<string, IEnumerable<Common.Entity.Reference>>> ProjectToSymbolToReferences;
        private object ProjectToSymbolToReferencesLock = new object();
        public Dictionary<string, Dictionary<string, IEnumerable<Common.Entity.Reference>>> GetProjectToSymbolToReferences()
        {
            if (ProjectToSymbolToReferences == null)
                lock (ProjectToSymbolToReferencesLock)
                    if (ProjectToSymbolToReferences == null)
                        ProjectToSymbolToReferences = ProjectManagers
                .SelectMany(pm => pm.GetOutgoingReferencesToSymbolIds())
                .GroupBy(t => t.Item1.ToAssemblyId)
                .ToDictionary(g => g.Key, g => g.GroupBy(t => t.Item2).ToDictionary(g2 => g2.Key, g2 => g2.Select(t2 => t2.Item1)));
            return ProjectToSymbolToReferences;
        }

        public string SolutionDestinationFolder { get; private set; }

        public bool ReferencesExists(string assemblyId, string symbolId)
        {
            return GetProjectToSymbolToReferences().Any(kvp => ReferencesExist(assemblyId, symbolId, kvp.Value));
        }

        private static bool ReferencesExist(string assemblyId, string symbolId, Dictionary<string, IEnumerable<Common.Entity.Reference>> referencesBySymbol)
        {
            IEnumerable<Common.Entity.Reference> references;
            return
                referencesBySymbol.TryGetValue(symbolId, out references)
            && references.Any(r => r.ToAssemblyId == assemblyId);
        }

        public bool UrlExists(string url)
        {
            return File.Exists(Path.Combine(SolutionDestinationFolder, url));
        }
        public StreamWriter GetSolutionExplorerWriter()
        {
            return new StreamWriter(Path.Combine(SolutionDestinationFolder, Constants.SolutionExplorer + ".html"));
        }

        public void WriteResults(string content)
        {
            File.WriteAllText(Path.Combine(SolutionDestinationFolder, "results.html"), content);
        }

        public void WriteAggregateStats(string content)
        {
            File.WriteAllText(Path.Combine(SolutionDestinationFolder, Constants.ProjectInfoFileName + ".txt"), content, Encoding.UTF8);
        }

        public void CreateMasterDeclarationsIndex(IEnumerable<IEnumerable<Common.Entity.DeclaredSymbolInfo>> declaredSymbolInfoSets)
        {
            var declaredSymbols = new List<Common.Entity.DeclaredSymbolInfo>();
            ////var declaredTypes = new List<DeclaredSymbolInfo>();

            using (Common.Measure.Time("Merging declared symbols"))
            {
                ushort assemblyNumber = 0;
                foreach (var declaredSymbolInfoSet in declaredSymbolInfoSets)
                {
                    foreach (var symbolInfo in declaredSymbolInfoSet)
                    {
                        symbolInfo.AssemblyNumber = assemblyNumber;
                        declaredSymbols.Add(symbolInfo);

                        ////if (SymbolKindText.IsType(symbolInfo.Kind))
                        ////{
                        ////    declaredTypes.Add(symbolInfo);
                        ////}
                    }

                    assemblyNumber++;
                }
            }

            WriteDeclaredSymbols(declaredSymbols);
            ////NamespaceExplorer.WriteNamespaceExplorer(declaredTypes, outputPath ?? rootPath);
        }

        public void WriteProjectMap(
            IEnumerable<Tuple<string, string>> listOfAssemblyNamesAndProjects,
            IDictionary<string, int> referencingAssembliesCount)
        {
            IEnumerable<Tuple<string, int>> assemblies;
            IEnumerable<string> projects;
            using (Common.Measure.Time("Normalizing..."))
            {
                Normalize(listOfAssemblyNamesAndProjects, out assemblies, out projects);
            }

            using (Common.Measure.Time("Writing project map"))
            {
                string masterAssemblyMap = Path.Combine(SolutionDestinationFolder, Constants.MasterAssemblyMap + ".txt");
                File.WriteAllLines(
                    masterAssemblyMap,
                    assemblies.Select(
                        t => t.Item1 + ";" +
                        t.Item2.ToString() + ";" +
                        (referencingAssembliesCount.ContainsKey(t.Item1) ? referencingAssembliesCount[t.Item1] : 0)),
                    Encoding.UTF8);

                string masterProjectMap = Path.Combine(SolutionDestinationFolder, Constants.MasterProjectMap + ".txt");
                File.WriteAllLines(
                    masterProjectMap,
                    projects,
                    Encoding.UTF8);
            }
        }

        public void AppendReferences(IEnumerable<KeyValuePair<string, IEnumerable<KeyValuePair<string, List<Common.Entity.Reference>>>>> references)
        {
            Common.Log.Write("References data files...", ConsoleColor.White);

            foreach (var referencesFromAssembly in references)
            {
                GetProjectManager(referencesFromAssembly.Key).AppendReferences(referencesFromAssembly.Value);
            }
        }

        public void AppendReferences(IEnumerable<KeyValuePair<string, Dictionary<string, List<Common.Entity.Reference>>>> references)
        {
            Common.Log.Write("References data files...", ConsoleColor.White);

            foreach (var referencesFromAssembly in references)
            {
                GetProjectManager(referencesFromAssembly.Key).AppendReferences(referencesFromAssembly.Value);
            }
        }

        public void AppendProcessedAssembly(string assemblyName)
        {
            File.AppendAllText(Path.Combine(SolutionDestinationFolder, @"ProcessedAssemblies.txt"), assemblyName + Environment.NewLine, Encoding.UTF8);
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

        public string GetCssPathFromFile(string fileName)
        {
            string result = MakeRelativeToFile(SolutionDestinationFolder, fileName);
            result = Path.Combine(result, "styles.css");
            result = result.Replace('\\', '/');
            return result;
        }

        private void WriteDeclaredSymbols(List<Common.Entity.DeclaredSymbolInfo> declaredSymbols)
        {
            if (declaredSymbols.Count == 0)
            {
                return;
            }

            using (Common.Measure.Time("Writing declared symbols"))
            {
                string masterIndexFile = Path.Combine(SolutionDestinationFolder, Constants.MasterIndexFileName);
                string huffmanFile = Path.Combine(SolutionDestinationFolder, Constants.HuffmanFileName);

                using (Common.Measure.Time("Sorting symbols"))
                {
                    declaredSymbols.Sort();
                }

                Common.Huffman huffman = null;
                using (Common.Measure.Time("Creating Huffman tables"))
                {
                    huffman = Common.Huffman.Create(declaredSymbols.Select(d => d.Description));

                    using (var fileStream = new FileStream(
                                    huffmanFile,
                                    FileMode.Create,
                                    FileAccess.Write,
                                    FileShare.None,
                                    262144,
                                    FileOptions.SequentialScan))
                    using (var hw = new HuffmanWriter(fileStream))
                    {
                        hw.Write(huffman);
                    }
                }

                using (Common.Measure.Time("Writing declared symbols to disk..."))
                using (var fileStream = new FileStream(
                    masterIndexFile,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    262144,
                    FileOptions.SequentialScan))
                using (var writer = new BinaryWriter(fileStream))
                {
                    writer.Write(declaredSymbols.Count);
                    using (var dsiWriter = new DeclaredSymbolInfoWriter(writer, huffman))
                    {
                        foreach (var declaredSymbol in declaredSymbols)
                        {
                            dsiWriter.Write(declaredSymbol);
                        }
                    }
                }
            }
        }

        private static void Normalize(
        IEnumerable<Tuple<string, string>> listOfAssemblyNamesAndProjects,
        out IEnumerable<Tuple<string, int>> assemblies,
        out IEnumerable<string> projects)
        {
            listOfAssemblyNamesAndProjects = listOfAssemblyNamesAndProjects
                .OrderBy(t => t.Item1, StringComparer.OrdinalIgnoreCase);

            var projectList = listOfAssemblyNamesAndProjects
                .Select((t, i) => Tuple.Create(t.Item2, i))
                .Where(t => !string.IsNullOrEmpty(t.Item1))
                .OrderBy(t => t.Item1, StringComparer.OrdinalIgnoreCase);

            var assemblyList = listOfAssemblyNamesAndProjects
                .Select((t, i) => Tuple.Create(t.Item1, -1))
                .ToArray();
            int j = 0;
            foreach (var index in projectList.Select(t => t.Item2))
            {
                assemblyList[index] = Tuple.Create(assemblyList[index].Item1, j++);
            }

            assemblies = assemblyList;
            projects = projectList.Select(t => t.Item1);
        }
    }
}
