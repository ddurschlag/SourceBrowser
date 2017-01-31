using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Path = System.IO.Path;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class ProjectGenerator
    {
        private readonly string assemblyAttributesFileName;

        public Project Project { get; private set; }
        public SymbolIndex SymbolIDToListOfLocationsMap { get; private set; }
        public ICollection<ISymbol> DeclaredSymbols { get; private set; }
        public Dictionary<ISymbol, ISymbol> BaseMembers { get; private set; }
        public MultiDictionary<ISymbol, ISymbol> ImplementedInterfaceMembers { get; set; }

        public IO.ProjectManager IOManager { get; private set; }
        public string AssemblyName { get; private set; }
        public SolutionGenerator SolutionGenerator { get; private set; }
        public string ProjectSourcePath { get; set; }
        public string ProjectFilePath { get; private set; }
        public List<string> OtherFiles { get; set; }
        public IEnumerable<MEF.ISymbolVisitor> PluginSymbolVisitors { get; private set; }
        public IEnumerable<MEF.ITextVisitor> PluginTextVisitors { get; private set; }

        public ProjectGenerator(SolutionGenerator solutionGenerator, Project project)
        {
            this.SymbolIDToListOfLocationsMap = new SymbolIndex();
            this.OtherFiles = new List<string>();
            this.SolutionGenerator = solutionGenerator;
            this.Project = project;
            this.ProjectFilePath = project.FilePath ?? solutionGenerator.ProjectFilePath;
            this.DeclaredSymbols = new HashSet<ISymbol>();
            this.BaseMembers = new Dictionary<ISymbol, ISymbol>();
            this.ImplementedInterfaceMembers = new MultiDictionary<ISymbol, ISymbol>();
            this.assemblyAttributesFileName = MetadataAsSource.GeneratedAssemblyAttributesFileName + (project.Language == LanguageNames.CSharp ? ".cs" : ".vb");
            PluginSymbolVisitors = SolutionGenerator.PluginAggregator.ManufactureSymbolVisitors(project).ToArray();
            PluginTextVisitors = SolutionGenerator.PluginAggregator.ManufactureTextVisitors(project).ToArray();
        }

        public async Task Generate()
        {
            try
            {
                if (string.IsNullOrEmpty(ProjectFilePath))
                {
                    Log.Exception("ProjectFilePath is empty: " + Project.ToString());
                    return;
                }

                if (!TrySetAssemblyName(Project))
                {
                    Log.Exception("Errors evaluating project: " + Project.Id);
                    return;
                }

                IOManager = SolutionGenerator.IOManager.GetProjectManager(AssemblyName);

                ProjectSourcePath = Paths.MakeRelativeToFolder(ProjectFilePath, SolutionGenerator.SolutionSourceFolder);

                if (IOManager.DestinationExists(new IO.Destination(new string[0], Constants.DeclaredSymbolsFileName + ".txt")))
                {
                    // apparently someone already generated a project with this assembly name - their assembly wins
                    Log.Exception(string.Format(
                        "A project with assembly name {0} was already generated, skipping current project: {1}",
                        this.AssemblyName,
                        this.ProjectFilePath), isSevere: false);
                    return;
                }

                if (Configuration.CreateFoldersOnDisk)
                {
                    IOManager.CreateDirectory();
                }

                var documents = Project.Documents.Where(IncludeDocument).ToList();

                var generationTasks = Partitioner.Create(documents)
                    .GetPartitions(Configuration.Parallelism)
                    .Select(partition =>
                        Task.Run(async () =>
                        {
                            using (partition)
                            {
                                while (partition.MoveNext())
                                {
                                    await GenerateDocument(partition.Current);
                                }
                            }
                        }));

                await Task.WhenAll(generationTasks);

                foreach (var document in documents)
                {
                    OtherFiles.Add(Paths.GetRelativeFilePathInProject(document));
                }

                if (Configuration.WriteProjectAuxiliaryFilesToDisk)
                {
                    GenerateProjectFile();
                    GenerateDeclarations();
                    GenerateBaseMembers();
                    GenerateImplementedInterfaceMembers();
                    GenerateProjectInfo();
                    GenerateReferencesDataFiles();
                    GenerateSymbolIDToListOfDeclarationLocationsMap(SymbolIDToListOfLocationsMap);
                    GenerateReferencedAssemblyList();
                    GenerateUsedReferencedAssemblyList();
                    GenerateProjectExplorer();
                    GenerateNamespaceExplorer();
                    GenerateIndex();
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "Project generation failed for: " + ProjectSourcePath);
            }
        }

        private void GenerateNamespaceExplorer()
        {
            Log.Write("Namespace Explorer...");
            var symbols = this.DeclaredSymbols.OfType<INamedTypeSymbol>()
                .Select(s => new Utilities.DeclaredSymbolInfoFactory().Manufacture(s, this.AssemblyName));
            new NamespaceExplorer(this.AssemblyName, IOManager).WriteNamespaceExplorer(symbols);
        }

        private Task GenerateDocument(Document document)
        {
            try
            {
                var documentGenerator = new DocumentGenerator(this, document);
                return documentGenerator.Generate();
            }
            catch (Exception e)
            {
                Log.Exception(e, "Document generation failed for: " + (document.FilePath ?? document.ToString()));
                return Task.FromResult(e);
            }
        }

        private void GenerateIndex()
        {
            Log.Write("Index.html...");
            var sb = new StringBuilder();
            Markup.WriteProjectIndex(sb, Project.AssemblyName);
            IOManager.WriteIndex(sb.ToString());
        }

        private bool IsCSharp
        {
            get
            {
                return Project.Language == LanguageNames.CSharp;
            }
        }

        private bool IncludeDocument(Document document)
        {
            if (document.Name == assemblyAttributesFileName)
            {
                return false;
            }

            return true;
        }

        private bool TrySetAssemblyName(Project project)
        {
            var assemblyName = project.AssemblyName;
            if (assemblyName == "<Error>")
            {
                AssemblyName = null;
                return false;
            }

            AssemblyName = SymbolIdService.GetAssemblyId(assemblyName);
            return true;
        }

        private static string GetProjectDestinationPath(string assemblyId, string solutionDestinationPath)
        {
            string subfolder = Path.Combine(solutionDestinationPath, assemblyId);
            return subfolder;
        }
    }
}
