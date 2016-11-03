﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            var projects = new List<string>();
            var properties = new Dictionary<string, string>();
            var emitAssemblyList = false;
            bool? force = null;
            var noBuiltInFederations = false;
            var offlineFederations = new Dictionary<string, string>();
            var federations = new HashSet<string>();
            bool? forceContinue = null;
            bool waitAtEnd = false;

            foreach (var arg in args)
            {
                if (arg.StartsWith("/out:"))
                {
                    Paths.SolutionDestinationFolder = Path.GetFullPath(arg.Substring("/out:".Length).StripQuotes());
                    continue;
                }

                if (arg == "/force")
                {
                    force = true;
                    continue;
                }

                if (arg == "/unforce")
                {
                    force = false;
                    continue;
                }

                if (arg.StartsWith("/in:"))
                {
                    string inputPath = arg.Substring("/in:".Length).StripQuotes();
                    try
                    {
                        if (!File.Exists(inputPath))
                        {
                            continue;
                        }

                        string[] paths = File.ReadAllLines(inputPath);
                        foreach (string path in paths)
                        {
                            AddProject(projects, path);
                        }
                    }
                    catch
                    {
                        Log.Write("Invalid argument: " + arg, ConsoleColor.Red);
                    }

                    continue;
                }

                if (arg.StartsWith("/p:"))
                {
                    var match = Regex.Match(arg, "/p:(?<name>[^=]+)=(?<value>.+)");
                    if (match.Success)
                    {
                        var propertyName = match.Groups["name"].Value;
                        var propertyValue = match.Groups["value"].Value;
                        properties.Add(propertyName, propertyValue);
                        continue;
                    }
                }

                if (arg == "/assemblylist")
                {
                    emitAssemblyList = true;
                    continue;
                }

                if (arg == "/nobuiltinfederations")
                {
                    noBuiltInFederations = true;
                    Log.Message("Disabling built-in federations.");
                    continue;
                }

                if (arg.StartsWith("/federation:"))
                {
                    string server = arg.Substring("/federation:".Length);
                    Log.Message($"Adding federation '{server}'.");
                    federations.Add(server);
                    continue;
                }

                if (arg.StartsWith("/offlinefederation:"))
                {
                    var match = Regex.Match(arg, "/offlinefederation:(?<server>[^=]+)=(?<file>.+)");
                    if (match.Success)
                    {
                        var server = match.Groups["server"].Value;
                        var assemblyListFileName = match.Groups["file"].Value;
                        offlineFederations[server] = assemblyListFileName;
                        Log.Message($"Adding federation '{server}' (offline from '{assemblyListFileName}').");
                        continue;
                    }

                    continue;
                }

                if (arg == "/continue")
                {
                    forceContinue = true;
                    continue;
                }

                if (arg == "/uncontinue")
                {
                    forceContinue = false;
                    continue;
                }

                if (arg == "/wait")
                {
                    waitAtEnd = true;
                    continue;
                }

                try
                {
                    AddProject(projects, arg);
                }
                catch (Exception ex)
                {
                    Log.Write("Exception: " + ex.ToString(), ConsoleColor.Red);
                }
            }

            if (projects.Count == 0)
            {
                PrintUsage();
                return;
            }

            AssertTraceListener.Register();
            AppDomain.CurrentDomain.FirstChanceException += FirstChanceExceptionHandler.HandleFirstChanceException;

            if (Paths.SolutionDestinationFolder == null)
            {
                Paths.SolutionDestinationFolder = Path.Combine(Microsoft.SourceBrowser.Common.Paths.BaseAppFolder, "Index");
            }

            Log.ErrorLogFilePath = Path.Combine(Paths.SolutionDestinationFolder, Log.ErrorLogFile);
            Log.MessageLogFilePath = Path.Combine(Paths.SolutionDestinationFolder, Log.MessageLogFile);

            // Warning, this will delete and recreate your destination folder
            Paths.PrepareDestinationFolder(force, forceContinue);

            using (Disposable.Timing("Generating website"))
            {
                var federation = noBuiltInFederations ? new Federation(null) : new Federation(federations);
                foreach (var entry in offlineFederations)
                {
                    federation.AddFederation(entry.Key, entry.Value);
                }

                IndexSolutions(projects, properties, federation);
                FinalizeProjects(emitAssemblyList, federation);
            }
            if (waitAtEnd)
            {
                Console.ReadLine();
            }
        }

        private static void AddProject(List<string> projects, string path)
        {
            var project = Path.GetFullPath(path);
            if (IsSupportedProject(project))
            {
                projects.Add(project);
            }
            else
            {
                Log.Exception("Project not found or not supported: " + path, isSevere: false);
            }
        }

        private static bool IsSupportedProject(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            return filePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                   filePath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase);
        }

        private static void PrintUsage()
        {
            Console.WriteLine(@"Usage: HtmlGenerator "
                + @"[/out:<outputdirectory>] "
                + @"[/force] "
                + @"<pathtosolution1.csproj|vbproj|sln> [more solutions/projects..] "
                + @"[/in:<filecontaingprojectlist>] "
                + @"[/nobuiltinfederations] "
                + @"[/offlinefederation:server=assemblyListFile] "
                + @"[/assemblylist]");
        }

        private static readonly Folder<Project> mergedSolutionExplorerRoot = new Folder<Project>();

        private static void IndexSolutions(IEnumerable<string> solutionFilePaths, Dictionary<string, string> properties, Federation federation)
        {
            var assemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in solutionFilePaths)
            {
                using (Disposable.Timing("Reading assembly names from " + path))
                {
                    foreach (var assemblyName in AssemblyNameExtractor.GetAssemblyNames(path))
                    {
                        assemblyNames.Add(assemblyName);
                    }
                }
            }

            foreach (var path in solutionFilePaths)
            {
                using (Disposable.Timing("Generating " + path))
                {
                    using (var solutionGenerator = new SolutionGenerator(
                        path,
                        Paths.SolutionDestinationFolder,
                        properties: properties.ToImmutableDictionary(),
                        federation: federation))
                    {
                        solutionGenerator.GlobalAssemblyList = assemblyNames;
                        solutionGenerator.Generate(solutionExplorerRoot: mergedSolutionExplorerRoot);
                    }
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        private static void FinalizeProjects(bool emitAssemblyList, Federation federation)
        {
            GenerateLooseFilesProject(Constants.MSBuildFiles, Paths.SolutionDestinationFolder);
            GenerateLooseFilesProject(Constants.TypeScriptFiles, Paths.SolutionDestinationFolder);
            using (Disposable.Timing("Finalizing references"))
            {
                try
                {
                    var solutionFinalizer = new SolutionFinalizer(Paths.SolutionDestinationFolder);
                    solutionFinalizer.FinalizeProjects(emitAssemblyList, federation, mergedSolutionExplorerRoot);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "Failure while finalizing projects");
                }
            }
        }

        private static void GenerateLooseFilesProject(string projectName, string solutionDestinationPath)
        {
            var projectGenerator = new ProjectGenerator(projectName, solutionDestinationPath);
            projectGenerator.GenerateNonProjectFolder();
        }
    }
}
