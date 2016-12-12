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
        internal ProjectManager(SolutionManager parent, string projectDestinationFolder, string assemblyId, bool writeDocumentsToDisk, int parallelism)
        {
            Parent = parent;
            ProjectDestinationFolder = projectDestinationFolder;
            AssemblyId = assemblyId;
            WriteDocumentsToDisk = writeDocumentsToDisk;
            Parallelism = parallelism;
        }
        public string ProjectDestinationFolder { get; private set; }
        public bool WriteDocumentsToDisk { get; private set; }
        public SolutionManager Parent { get; private set; }
        public string AssemblyId { get; private set; }
        public int Parallelism { get; private set; }

        public bool HtmlDestinationExists(Destination d)
        {
            return File.Exists(GetHtmlDestinationPath(d));
        }

        public bool DestinationExists(Destination d)
        {
            return File.Exists(GetDestinationPath(d));
        }

        public IEnumerable<string> CallTypescriptAnalyzer(string argumentJson)
        {
            var output = Path.Combine(Common.Paths.BaseAppFolder, "output");
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }

            var argumentsPath = Path.Combine(Common.Paths.BaseAppFolder, "TypeScriptAnalyzerArguments.json");
            File.WriteAllText(argumentsPath, argumentJson);

            var analyzerJs = Path.Combine(Common.Paths.BaseAppFolder, @"TypeScriptSupport\analyzer.js");
            var arguments = string.Format("\"{0}\" {1}", analyzerJs, argumentsPath);

            Common.ProcessLaunchService.ProcessRunResult result;
            try
            {
                using (Common.Disposable.Timing("Calling Node.js to process TypeScript"))
                {
                    result = new Common.ProcessLaunchService().RunAndRedirectOutput("node", arguments);
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Common.Log.Write("Warning: Node.js is required to generate TypeScript files. Skipping generation. Download Node.js from https://nodejs.org.", ConsoleColor.Yellow);
                Common.Log.Exception("Node.js is not installed.");
                yield break;
            }

            using (Common.Disposable.Timing("Generating TypeScript files"))
            {
                foreach (var file in Directory.GetFiles(output))
                {
                    if (Path.GetFileNameWithoutExtension(file) == "ok")
                    {
                        continue;
                    }

                    if (Path.GetFileNameWithoutExtension(file) == "error")
                    {
                        var errorContent = File.ReadAllText(file);
                        Common.Log.Exception(DateTime.Now.ToString() + " " + errorContent);
                        continue;
                    }

                    yield return File.ReadAllText(file);
                }
            }
        }

        public void WriteOnce(string path, Action<StringBuilder> builder)
        {
            if (!File.Exists(path))
            {
                StringBuilder sb = new StringBuilder();
                var folder = Path.GetDirectoryName(path);
                Directory.CreateDirectory(folder);
                builder(sb);
                File.WriteAllText(path, sb.ToString());
            }
        }

        [Obsolete("This interface seems far too broad")]
        public string GetFileText(string path)
        {
            return File.ReadAllText(path);
        }

        [Obsolete("This interface seems far too broad")]
        public int GetFileLineCount(string path)
        {
            return File.ReadAllLines(path).Length;
        }

        public string[] ReadProjectExplorer()
        {
            var fileName = Path.Combine(ProjectDestinationFolder, Constants.ProjectExplorer + ".html");
            if (!File.Exists(fileName))
            {
                return null;
            }

            return File.ReadAllLines(fileName);
        }

        public void CreateDirectory()
        {
            Directory.CreateDirectory(ProjectDestinationFolder);
        }

        public void CreateDirectory(Destination d)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GetHtmlDestinationPath(d)));
        }

        public void CreateReferencesDirectory()
        {
            Directory.CreateDirectory(Path.Combine(ProjectDestinationFolder, Constants.ReferencesFileName));
        }

        public IEnumerable<string> GetHtmlOutputFiles()
        {
            return Directory.GetFiles(ProjectDestinationFolder, "*.html", SearchOption.AllDirectories);
        }

        public IEnumerable<SymbolReferencesThingy> GetReferencesFiles()
        {
            return Directory.GetFiles(Path.Combine(ProjectDestinationFolder, Constants.ReferencesFileName), "*.txt")
                .Select(fp=>new SymbolReferencesThingy(fp));
        }

        public IEnumerable<string> GetDeclaredSymbolLines()
        {
            return GetTextFileLines(Constants.DeclaredSymbolsFileName);
        }

        public IEnumerable<string> GetBaseMemberLines()
        {
            return GetTextFileLines(Constants.BaseMembersFileName);
        }

        public IEnumerable<string> GetImplementedInterfaceMemberLines()
        {
            return GetTextFileLines(Constants.ImplementedInterfaceMembersFileName);
        }

        public IEnumerable<string> GetProjectInfoLines()
        {
            return GetTextFileLines(Constants.ProjectInfoFileName);
        }

        public IEnumerable<string> GetReferencedAssemblyLines()
        {
            return GetTextFileLines(Constants.ReferencedAssemblyList);
        }

        public Common.SymbolIndex GetDeclarationMap()
        {
            return ReadSymbolIndex(Constants.DeclarationMap);
        }

        private Common.SymbolIndex ReadSymbolIndex(string file)
        {
            Common.SymbolIndexBuilder builder = new Common.SymbolIndexBuilder();

            foreach (var line in GetTextFileLines(file))
                builder.Process(line);

            return builder.Build();
        }

        private IEnumerable<string> GetTextFileLines(string file)
        {
            var assemblyIndex = Path.Combine(ProjectDestinationFolder, file + ".txt");
            if (!File.Exists(assemblyIndex))
            {
                return new string[0];
            }

            return File.ReadAllLines(assemblyIndex);
        }

        [Obsolete("This interface seems far too broad")]
        public void Write(string file, string contents)
        {
            File.WriteAllText(Path.Combine(ProjectDestinationFolder, file), contents);
        }

        public StreamWriter GetNamespaceWriter()
        {
            var fileName = Path.Combine(ProjectDestinationFolder, Constants.Namespaces);
            return new StreamWriter(fileName);
        }

        public void WriteIDResolvingFileOnce(Action<StringBuilder> builder)
        {
            WriteOnce(Path.Combine(ProjectDestinationFolder, Constants.IDResolvingFileName + ".html"), builder);
        }

        public StreamWriter GetIDResolvingWriter(string suffix)
        {
            var fileName = Path.Combine(ProjectDestinationFolder, Constants.IDResolvingFileName + suffix + ".html");

            File.Delete(fileName);
            return new StreamWriter(fileName, append: false, encoding: Encoding.UTF8);
        }

        public ReferenceWriter GetReferenceWriter(string symbolId)
        {
            return new ReferenceWriter(new StreamWriter(GetReferencesFilePath(symbolId + ".txt"), append: true, encoding: Encoding.UTF8));
        }

        public bool ReferencesExists(string symbolId)
        {
            return File.Exists(GetReferencesFilePath(symbolId + ".txt"));
        }

        public bool ReferenceDirExists()
        {
            return Directory.Exists(Path.Combine(ProjectDestinationFolder, Constants.ReferencesFileName));
        }

        public void Patch(Destination d, byte[] zeroId, IEnumerable<long> offsets)
        {
            int zeroIdLength = zeroId.Length;

            using (var stream = new FileStream(GetHtmlDestinationPath(d), FileMode.Open, FileAccess.ReadWrite))
            {
                foreach (var offset in offsets)
                {
                    stream.Seek(offset, SeekOrigin.Begin);
                    stream.Write(zeroId, 0, zeroIdLength);
                }
            }
        }

        public void AppendReferences(IEnumerable<KeyValuePair<string, List<Common.Entity.Reference>>> references)
        {
            CreateReferencesDirectory();

            Parallel.ForEach(
                references,
                new ParallelOptions { MaxDegreeOfParallelism = Parallelism },
                referencesToSymbol =>
                {
                    try
                    {
                        using (var writer = GetReferenceWriter(referencesToSymbol.Key))
                        {
                            foreach (var reference in referencesToSymbol.Value)
                            {
                                writer.Write(reference);
                            }
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        Common.Log.Exception(ex.ToString() + "\r\n\r\n" + "AssemblyId: " + AssemblyId + "   referencesToSymbol.Key: " + referencesToSymbol.Key);
                    }
                });
        }

        public StreamWriter GetHtmlWriter(Destination d)
        {
            if (WriteDocumentsToDisk)
                return new StreamWriter(
                    GetHtmlDestinationPath(d),
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
            var destinationPath = GetHtmlDestinationPath(d);
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

        //TODO: Should this return a destination?par
        public string GetReferencedSymbolDestinationFilePath(string symbolId)
        {
            return Path.Combine(
                ProjectDestinationFolder,
                Constants.PartialResolvingFileName,
                symbolId);
        }

        public string GetPathToSolutionRoot(Destination d)
        {
            var relativePath = GetHtmlDestinationPath(d).Substring(Parent.SolutionDestinationFolder.Length + 1);
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
            return GetHtmlDestinationPath(d);
        }

        private string GetHtmlDestinationPath(Destination d)
        {
            return Path.GetFullPath(Path.Combine(
                new string[] { ProjectDestinationFolder }
                .Concat(d.Folders)
                .Concat(new string[] { d.FileName })
                .ToArray()
            ) + ".html");
        }

        private string GetDestinationPath(Destination d)
        {
            return Path.GetFullPath(Path.Combine(
                new string[] { ProjectDestinationFolder }
                .Concat(d.Folders)
                .Concat(new string[] { d.FileName }).ToArray()
            ));
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
                GetHtmlDestinationPath(d)
            ).Replace('\\', '/');
        }

        private string GetReferencesFilePath(string filename)
        {
            return Path.Combine(ProjectDestinationFolder, Constants.ReferencesFileName, filename);
        }

        public void EnsureReferencesFile(string name)
        {
            File.AppendAllText(Path.Combine(ProjectDestinationFolder, name + ".txt"), string.Empty);
        }

        public void WriteDeclaredSymbols(IEnumerable<string> lines, bool overwrite = false)
        {
            var fileName = Path.Combine(ProjectDestinationFolder, Constants.DeclaredSymbolsFileName + ".txt");
            if (overwrite)
            {
                File.WriteAllLines(fileName, lines, Encoding.UTF8);
            }
            else
            {
                File.AppendAllLines(fileName, lines, Encoding.UTF8);
            }
        }

        public void WriteBaseMembers(IEnumerable<string> baseMemberLines)
        {
            var fileName = Path.Combine(ProjectDestinationFolder, Constants.BaseMembersFileName + ".txt");
            File.WriteAllLines(fileName, baseMemberLines);
        }

        public void WriteReferencingAssemblies(IEnumerable<string> referencingAssemblyLines)
        {
            var fileName = Path.Combine(ProjectDestinationFolder, Constants.ReferencingAssemblyList + ".txt");
            File.WriteAllLines(fileName, referencingAssemblyLines);
        }

        public void WriteProjectExplorer(string content)
        {
            File.WriteAllText(Path.Combine(ProjectDestinationFolder, Constants.ProjectExplorer) + ".html", content);
        }

        public void WriteProjectInfo(string content)
        {
            File.WriteAllText(Path.Combine(ProjectDestinationFolder, Constants.ProjectInfoFileName) + ".txt", content);
        }

        public void WriteImplementedInterfaceMembers(IEnumerable<string> implementedInterfaceMemberLines)
        {
            var fileName = Path.Combine(ProjectDestinationFolder, Constants.ImplementedInterfaceMembersFileName + ".txt");
            File.WriteAllLines(fileName, implementedInterfaceMemberLines);
        }

        public void WriteDeclarationsMap(Common.SymbolIndex si, bool overwrite = false)
        {
            var fileName = Path.Combine(ProjectDestinationFolder, Constants.DeclarationMap + ".txt");
            using (var writer = new StreamWriter(fileName, append: !overwrite, encoding: Encoding.UTF8))
            {
                foreach (var kvp in si)
                {
                    writer.WriteLine("=" + kvp.Item1);
                    foreach (var location in kvp.Item2)
                    {
                        Common.SymbolLocation.Write(writer, location);
                    }
                }
            }
        }

        private static string partialTypeDisambiguationFileTemplate = @"<!DOCTYPE html>
<html><head><link rel=""stylesheet"" href=""{0}"">
</head><body><div class=""partialTypeHeader"">Partial Type</div>
{1}
</body></html>";

        public void GeneratePartialTypeDisambiguationFile(string symbolId, IEnumerable<string> filePaths)
        {
            string partialFolder = Path.Combine(ProjectDestinationFolder, Constants.PartialResolvingFileName);
            Directory.CreateDirectory(partialFolder);
            var disambiguationFileName = Path.Combine(partialFolder, symbolId) + ".html";
            string list = string.Join(Environment.NewLine,
                filePaths
                .OrderBy(filePath => Path.ChangeExtension(filePath, null))
                .Select(filePath => "<a href=\"../" + filePath + ".html#" + symbolId + "\"><div class=\"partialTypeLink\">" + filePath + "</div></a>"));
            string content = string.Format(
                partialTypeDisambiguationFileTemplate,
                Parent.GetCssPathFromFile(disambiguationFileName),
                list);
            File.WriteAllText(disambiguationFileName, content, Encoding.UTF8);
        }
    }
}
