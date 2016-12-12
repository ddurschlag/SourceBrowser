using System;
using System.Collections.Generic;
using System.Globalization;
using Path = System.IO.Path;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class SolutionFinalizer
    {
        public const string PATCH_MARKER = "<!-- Patched -->";

        public string SolutionDestinationFolder;
        public IEnumerable<ProjectFinalizer> projects;
        public readonly Dictionary<string, ProjectFinalizer> assemblyNameToProjectMap = new Dictionary<string, ProjectFinalizer>();
        public IO.SolutionManager IOManager;

        public SolutionFinalizer(string rootPath, IO.SolutionManager ioManager)
        {
            this.SolutionDestinationFolder = rootPath;
            IOManager = ioManager;
            this.projects = DiscoverProjects()
                            .OrderBy(p => p.AssemblyId, StringComparer.OrdinalIgnoreCase)
                            .ToArray();
            //CalculateReferencingAssemblies();
        }

        private void CalculateReferencingAssemblies()
        {
            using (Disposable.Timing("Calculating referencing assemblies"))
            {
                foreach (var project in this.projects)
                {
                    assemblyNameToProjectMap.Add(project.AssemblyId, project);
                }

                foreach (var project in this.projects)
                {
                    if (project.ReferencedAssemblies != null)
                    {
                        foreach (var reference in project.ReferencedAssemblies)
                        {
                            ProjectFinalizer referencedProject = null;
                            if (assemblyNameToProjectMap.TryGetValue(reference, out referencedProject))
                            {
                                referencedProject.ReferencingAssemblies.Add(project.AssemblyId);
                            }
                        }
                    }
                }

                var mostReferencedProjects = projects
                    .OrderByDescending(p => p.ReferencingAssemblies.Count)
                    .Select(p => p.AssemblyId + ";" + p.ReferencingAssemblies.Count)
                    .Take(100)
                    .ToArray();

                //todo: This should be logging
                //var filePath = Path.Combine(this.SolutionDestinationFolder, Constants.TopReferencedAssemblies + ".txt");
                //File.WriteAllLines(filePath, mostReferencedProjects);
            }
        }

        private IEnumerable<ProjectFinalizer> DiscoverProjects()
        {
            //var directories = Directory.GetDirectories(SolutionDestinationFolder);
            //foreach (var directory in directories)
            foreach (var pm in IOManager.ProjectManagers)
            {
                //                if (Directory.Exists(referenceDirectory))
                if (pm.ReferenceDirExists())
                {
                    ProjectFinalizer finalizer = null;
                    try
                    {
                        finalizer = new ProjectFinalizer(this, pm);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "Failure when creating a ProjectFinalizer for " + pm.AssemblyId);
                        finalizer = null;
                    }

                    if (finalizer != null)
                    {
                        yield return finalizer;
                    }
                }
            }
        }

        public void FinalizeProjects(bool emitAssemblyList, Federation federation, Folder<Project> solutionExplorerRoot = null)
        {
            IO.Utility.SortLines(Paths.ProcessedAssemblies);
            WriteSolutionExplorer(solutionExplorerRoot);
            CreateReferencesFiles();
            IOManager.CreateMasterDeclarationsIndex(this.projects.Select(p => p.DeclaredSymbols.Values));
            CreateProjectMap();
            CreateReferencingProjectLists();
            WriteAggregateStats();
            IO.Utility.DeployFilesToRoot(SolutionDestinationFolder, emitAssemblyList, federation.GetServers());

            if (emitAssemblyList)
            {
                var assemblyNames = projects
                    .Where(projectFinalizer => projectFinalizer.ProjectInfoLine != null)
                    .Select(projectFinalizer => projectFinalizer.AssemblyId).ToList();

                var sorter = GetCustomRootSorter();
                assemblyNames.Sort(sorter);

                Markup.GenerateResultsHtmlWithAssemblyList(IOManager, assemblyNames);
            }
            else
            {
                Markup.GenerateResultsHtml(IOManager);
            }
        }

        private static Comparison<string> GetCustomRootSorter()
        {
            var file = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "AssemblySortOrder.txt");
            if (!System.IO.File.Exists(file))
            {
                return (l, r) => StringComparer.OrdinalIgnoreCase.Compare(l, r);
            }

            var lines = System.IO.File
                .ReadAllLines(file)
                .Select((assemblyName, index) => new KeyValuePair<string, int>(assemblyName, index + 1))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return (l, r) =>
            {
                int index1, index2;
                lines.TryGetValue(l, out index1);
                lines.TryGetValue(r, out index2);
                if (index1 == 0 || index2 == 0)
                {
                    return l.CompareTo(r);
                }
                else
                {
                    return index1 - index2;
                }
            };
        }

        private void CreateReferencingProjectLists()
        {
            using (Disposable.Timing("Writing referencing assemblies"))
            {
                foreach (var project in this.projects)
                {
                    project.CreateReferencingProjectList();
                }
            }
        }

        private void WriteAggregateStats()
        {
            var sb = new StringBuilder();

            long totalProjects = 0;
            long totalDocumentCount = 0;
            long totalLinesOfCode = 0;
            long totalBytesOfCode = 0;
            long totalDeclaredSymbolCount = 0;
            long totalDeclaredTypeCount = 0;
            long totalPublicTypeCount = 0;

            foreach (var project in this.projects)
            {
                totalProjects++;
                totalDocumentCount += project.DocumentCount;
                totalLinesOfCode += project.LinesOfCode;
                totalBytesOfCode += project.BytesOfCode;
                totalDeclaredSymbolCount += project.DeclaredSymbolCount;
                totalDeclaredTypeCount += project.DeclaredTypeCount;
                totalPublicTypeCount += project.PublicTypeCount;
            }

            sb.AppendLine("ProjectCount=" + totalProjects.WithThousandSeparators());
            sb.AppendLine("DocumentCount=" + totalDocumentCount.WithThousandSeparators());
            sb.AppendLine("LinesOfCode=" + totalLinesOfCode.WithThousandSeparators());
            sb.AppendLine("BytesOfCode=" + totalBytesOfCode.WithThousandSeparators());
            sb.AppendLine("DeclaredSymbols=" + totalDeclaredSymbolCount.WithThousandSeparators());
            sb.AppendLine("DeclaredTypes=" + totalDeclaredTypeCount.WithThousandSeparators());
            sb.AppendLine("PublicTypes=" + totalPublicTypeCount.WithThousandSeparators());
            IOManager.WriteAggregateStats(sb.ToString());
        }

        private void CreateReferencesFiles()
        {
            Parallel.ForEach(
                projects,
                new ParallelOptions { MaxDegreeOfParallelism = Configuration.Parallelism },
                project =>
                {
                    try
                    {
                        project.CreateReferencesFiles();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "CreateReferencesFiles failed for project: " + project.AssemblyId);
                    }
                });
        }

        public void CreateProjectMap(string outputPath = null)
        {
            //This is already an array...
            //var projects = this.projects
            //    // can't exclude assemblies without project because symbols rely on assembly index
            //    // and they just take the index from this.projects (see below)
            //    //.Where(p => p.ProjectInfoLine != null) 
            //    .ToArray();

            IOManager.WriteProjectMap(
                projects.Select(p => Tuple.Create(p.AssemblyId, p.ProjectInfoLine)),
                projects.ToDictionary(p => p.AssemblyId, p => p.ReferencingAssemblies.Count)
            );
        }
    }
}
