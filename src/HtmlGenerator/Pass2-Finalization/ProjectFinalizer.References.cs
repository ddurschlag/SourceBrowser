﻿using System;
using System.Collections.Generic;
using Path = System.IO.Path;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SourceBrowser.Common;
using Microsoft.SourceBrowser.Common.Entity;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class ProjectFinalizer
    {
        public const string PATCH_MARKER = "<!-- Patched -->";

        public void CreateReferencesFiles(Dictionary<string,IEnumerable<Reference>> symbolIdToReferences)
        {
            BackpatchUnreferencedDeclarations();
            Markup.WriteRedirectFile(IOManager);
            GenerateFinalReferencesFiles(symbolIdToReferences);
        }

        public void GenerateFinalReferencesFiles(Dictionary<string, IEnumerable<Reference>> symbolIdToReferences)
        {
            if (!symbolIdToReferences.Any())
            {
                return;
            }

            Log.Write("Creating references files for " + this.AssemblyId);

            foreach ( var kvp in symbolIdToReferences )
            {
                string symbolName;
                var referenceKindGroups = CreateReferences(kvp.Value, out symbolName);
                string symbolId = kvp.Key;
                using (var writer = IOManager.GetReferenceHtmlWriter(symbolId))
                {
                    Markup.WriteReferencesFileHeader(writer, symbolName);

                    if (this.AssemblyId != Constants.MSBuildItemsAssembly &&
                        this.AssemblyId != Constants.MSBuildPropertiesAssembly &&
                        this.AssemblyId != Constants.MSBuildTargetsAssembly &&
                        this.AssemblyId != Constants.MSBuildTasksAssembly &&
                        this.AssemblyId != Constants.GuidAssembly)
                    {
                        var id = TextUtilities.HexStringToULong(symbolId);
                        WriteBaseMember(id, writer);
                        WriteImplementedInterfaceMembers(id, writer);
                    }

                    foreach (var referenceKind in referenceKindGroups.OrderBy(t => (int)t.Item1))
                    {
                        WriteHeader(symbolName, writer, CountItems(referenceKind), referenceKind.Item1);

                        foreach (var sameAssemblyReferencesGroup in referenceKind.Item2.OrderBy(g => g.Item1))
                        {
                            WriteAssemblyReferences(writer, sameAssemblyReferencesGroup);
                        }
                    }

                    Write(writer, "</body></html>");
                }
            }

           // Parallel.ForEach(
           //    references,
           //    new ParallelOptions { MaxDegreeOfParallelism = Configuration.Parallelism },
           //    referenceAndSymbol =>
           //{
           //    try
           //    {
           //        GenerateReferencesFile(referenceAndSymbol);
           //    }
           //    catch (Exception ex)
           //    {
           //        Log.Exception(ex, "Failed to generate references file for: " + referenceAndSymbol);
           //    }
           //});
        }

        private static string WriteHeader(string symbolName, System.IO.StreamWriter writer, int totalReferenceCount, ReferenceKind kind)
        {
            string formatString = GetFormatStringForKind(kind);
            string headerText = string.Format(
                formatString,
                totalReferenceCount,
                totalReferenceCount == 1 ? "" : "s",
                symbolName);

            Write(writer, string.Format(@"<div class=""rH"">{0}</div>", headerText));
            return formatString;
        }

        private static void WriteAssemblyReferences(System.IO.StreamWriter writer, Tuple<string, IEnumerable<Tuple<string, IEnumerable<IGrouping<int, Reference>>>>> sameAssemblyReferencesGroup)
        {
            string assemblyName = sameAssemblyReferencesGroup.Item1;
            Write(writer, "<div class=\"rA\">{0} ({1})</div>", assemblyName, CountItems(sameAssemblyReferencesGroup));
            Write(writer, "<div class=\"rG\" id=\"{0}\">", assemblyName);

            foreach (var sameFileReferencesGroup in sameAssemblyReferencesGroup.Item2.OrderBy(g => g.Item1))
            {
                WriteFileReferences(writer, sameFileReferencesGroup);
            }

            WriteLine(writer, "</div>");
        }

        private static void WriteFileReferences(System.IO.StreamWriter writer, Tuple<string, IEnumerable<IGrouping<int, Reference>>> sameFileReferencesGroup)
        {
            Write(writer, "<div class=\"rF\">");
            WriteLine(writer, "<div class=\"rN\">{0} ({1})</div>", sameFileReferencesGroup.Item1, CountItems(sameFileReferencesGroup));

            foreach (var sameLineReferencesGroup in sameFileReferencesGroup.Item2)
            {
                WriteLineReferences(writer, sameLineReferencesGroup.Key, sameLineReferencesGroup);
            }

            WriteLine(writer, "</div>");
        }

        private static void WriteLineReferences(System.IO.StreamWriter writer, int line, IEnumerable<Reference> sameLineReferences)
        {
            Write(writer, "<a href=\"{0}\">", sameLineReferences.First().Url);
            Write(writer, "<b>{0}</b>", line);
            MergeOccurrences(writer, sameLineReferences);
            WriteLine(writer, "</a>");
        }

        //private void GenerateReferencesFile(Tuple<Reference, string> referencesFile)
        //{
        //    string symbolName = null;
        //    var referenceKindGroups = CreateReferences(referencesFile, out symbolName);

        //    using (var writer = referencesFile.GetOutputWriter())
        //    {
        //        Markup.WriteReferencesFileHeader(writer, symbolName);

        //        if (this.AssemblyId != Constants.MSBuildItemsAssembly &&
        //            this.AssemblyId != Constants.MSBuildPropertiesAssembly &&
        //            this.AssemblyId != Constants.MSBuildTargetsAssembly &&
        //            this.AssemblyId != Constants.MSBuildTasksAssembly &&
        //            this.AssemblyId != Constants.GuidAssembly)
        //        {
        //            var id = TextUtilities.HexStringToULong(referencesFile.SymbolId);
        //            WriteBaseMember(id, writer);
        //            WriteImplementedInterfaceMembers(id, writer);
        //        }

        //        foreach (var referenceKind in referenceKindGroups.OrderBy(t => (int)t.Item1))
        //        {
        //            string formatString = "";

        //            var kind = referenceKind.Item1;
        //            formatString = GetFormatStringForKind(kind);

        //            var referencesOfSameKind = referenceKind.Item2.OrderBy(g => g.Item1);
        //            int totalReferenceCount = CountItems(referenceKind);
        //            string headerText = string.Format(
        //                formatString,
        //                totalReferenceCount,
        //                totalReferenceCount == 1 ? "" : "s",
        //                symbolName);

        //            Write(writer, string.Format(@"<div class=""rH"">{0}</div>", headerText));

        //            foreach (var sameAssemblyReferencesGroup in referencesOfSameKind)
        //            {
        //                string assemblyName = sameAssemblyReferencesGroup.Item1;
        //                Write(writer, "<div class=\"rA\">{0} ({1})</div>", assemblyName, CountItems(sameAssemblyReferencesGroup));
        //                Write(writer, "<div class=\"rG\" id=\"{0}\">", assemblyName);

        //                foreach (var sameFileReferencesGroup in sameAssemblyReferencesGroup.Item2.OrderBy(g => g.Item1))
        //                {
        //                    Write(writer, "<div class=\"rF\">");
        //                    WriteLine(writer, "<div class=\"rN\">{0} ({1})</div>", sameFileReferencesGroup.Item1, CountItems(sameFileReferencesGroup));

        //                    foreach (var sameLineReferencesGroup in sameFileReferencesGroup.Item2)
        //                    {
        //                        var url = sameLineReferencesGroup.First().Url;
        //                        Write(writer, "<a href=\"{0}\">", url);

        //                        Write(writer, "<b>{0}</b>", sameLineReferencesGroup.Key);
        //                        MergeOccurrences(writer, sameLineReferencesGroup);
        //                        WriteLine(writer, "</a>");
        //                    }

        //                    WriteLine(writer, "</div>");
        //                }

        //                WriteLine(writer, "</div>");
        //            }
        //        }

        //        Write(writer, "</body></html>");
        //    }

        //    //File.Delete(rawReferencesFile);
        //}

        private static string GetFormatStringForKind(ReferenceKind kind)
        {
            string formatString;
            switch (kind)
            {
                case ReferenceKind.Reference:
                    formatString = "{0} reference{1} to {2}";
                    break;
                case ReferenceKind.DerivedType:
                    formatString = "{0} type{1} derived from {2}";
                    break;
                case ReferenceKind.InterfaceInheritance:
                    formatString = "{0} interface{1} inheriting from {2}";
                    break;
                case ReferenceKind.InterfaceImplementation:
                    formatString = "{0} implementation{1} of {2}";
                    break;
                case ReferenceKind.Read:
                    formatString = "{0} read{1} of {2}";
                    break;
                case ReferenceKind.Write:
                    formatString = "{0} write{1} to {2}";
                    break;
                case ReferenceKind.Instantiation:
                    formatString = "{0} instantiation{1} of {2}";
                    break;
                case ReferenceKind.Override:
                    formatString = "{0} override{1} of {2}";
                    break;
                case ReferenceKind.InterfaceMemberImplementation:
                    formatString = "{0} implementation{1} of {2}";
                    break;
                case ReferenceKind.GuidUsage:
                    formatString = "{0} usage{1} of Guid {2}";
                    break;
                case ReferenceKind.EmptyArrayAllocation:
                    formatString = "{0} allocation{1} of empty arrays";
                    break;
                case ReferenceKind.MSBuildPropertyAssignment:
                    formatString = "{0} assignment{1} to MSBuild property {2}";
                    break;
                case ReferenceKind.MSBuildPropertyUsage:
                    formatString = "{0} usage{1} of MSBuild property {2}";
                    break;
                case ReferenceKind.MSBuildItemAssignment:
                    formatString = "{0} assignment{1} to MSBuild item {2}";
                    break;
                case ReferenceKind.MSBuildItemUsage:
                    formatString = "{0} usage{1} of MSBuild item {2}";
                    break;
                case ReferenceKind.MSBuildTargetDeclaration:
                    formatString = "{0} declaration{1} of MSBuild target {2}";
                    break;
                case ReferenceKind.MSBuildTargetUsage:
                    formatString = "{0} usage{1} of MSBuild target {2}";
                    break;
                case ReferenceKind.MSBuildTaskDeclaration:
                    formatString = "{0} import{1} of MSBuild task {2}";
                    break;
                case ReferenceKind.MSBuildTaskUsage:
                    formatString = "{0} call{1} to MSBuild task {2}";
                    break;
                default:
                    throw new NotImplementedException("Missing case for " + kind);
            }

            return formatString;
        }

        public void CreateReferencingProjectList()
        {
            //todo: What's this 100?
            if (ReferencingAssemblies.Count > 0 && ReferencingAssemblies.Count < 100)
            {
                IOManager.WriteReferencingAssemblies(ReferencingAssemblies);
                PatchProjectExplorer();
            }
        }

        private void PatchProjectExplorer()
        {
            if (ReferencingAssemblies.Count == 0 || ReferencingAssemblies.Count > 100)
            {
                return;
            }

            var sourceLines = IOManager.ReadProjectExplorer();

            if (sourceLines == null) return;

            //Check if already patched -- if so, skip it
            if (sourceLines.Length > 0 && sourceLines[0].Equals(PATCH_MARKER))
                return;

            List<string> lines = new List<string>(sourceLines.Length + ReferencingAssemblies.Count + 3);

            //Add marker to indicate this file has already been patched
            lines.Add(PATCH_MARKER);

            RelativeState state = RelativeState.Before;
            foreach (var sourceLine in sourceLines)
            {
                switch (state)
                {
                    case RelativeState.Before:
                        if (sourceLine == "<div class=\"folderTitle\">References</div><div class=\"folder\">")
                        {
                            state = RelativeState.Inside;
                        }

                        break;
                    case RelativeState.Inside:
                        if (sourceLine == "</div>")
                        {
                            state = RelativeState.InsertionPoint;
                        }

                        break;
                    case RelativeState.InsertionPoint:
                        lines.Add("<div class=\"folderTitle\">Used By</div><div class=\"folder\">");

                        foreach (var referencingAssembly in ReferencingAssemblies)
                        {
                            string referenceHtml = Markup.GetProjectExplorerReference("/#" + referencingAssembly, referencingAssembly);
                            lines.Add(referenceHtml);
                        }

                        lines.Add("</div>");

                        state = RelativeState.After;
                        break;
                    case RelativeState.After:
                        break;
                    default:
                        break;
                }

                lines.Add(sourceLine);
            }
            IOManager.WriteProjectExplorer(string.Join(Environment.NewLine, lines));
        }

        private enum RelativeState
        {
            Before,
            Inside,
            InsertionPoint,
            After
        }

        private void WriteImplementedInterfaceMembers(ulong symbolId, System.IO.StreamWriter writer)
        {
            HashSet<Tuple<string, ulong>> implementedInterfaceMembers;
            if (!ImplementedInterfaceMembers.TryGetValue(symbolId, out implementedInterfaceMembers))
            {
                return;
            }

            Write(writer, string.Format(@"<div class=""rH"">Implemented interface member{0}:</div>", implementedInterfaceMembers.Count > 1 ? "s" : ""));

            foreach (var implementedInterfaceMember in implementedInterfaceMembers)
            {
                var assemblyName = implementedInterfaceMember.Item1;
                var interfaceSymbolId = implementedInterfaceMember.Item2;

                ProjectFinalizer baseProject = null;
                if (!this.SolutionFinalizer.assemblyNameToProjectMap.TryGetValue(assemblyName, out baseProject))
                {
                    return;
                }

                DeclaredSymbolInfo symbol = null;
                if (baseProject.DeclaredSymbols.TryGetValue(interfaceSymbolId, out symbol))
                {
                    var sb = new StringBuilder();
                    Markup.WriteSymbol(symbol, sb);
                    writer.Write(sb.ToString());
                }
            }
        }

        private void WriteBaseMember(ulong symbolId, System.IO.StreamWriter writer)
        {
            Tuple<string, ulong> baseMemberLink;
            if (!BaseMembers.TryGetValue(symbolId, out baseMemberLink))
            {
                return;
            }

            Write(writer, @"<div class=""rH"">Base:</div>");

            var assemblyName = baseMemberLink.Item1;
            var baseSymbolId = baseMemberLink.Item2;

            ProjectFinalizer baseProject = null;
            if (!this.SolutionFinalizer.assemblyNameToProjectMap.TryGetValue(assemblyName, out baseProject))
            {
                return;
            }

            DeclaredSymbolInfo symbol = null;
            if (baseProject.DeclaredSymbols.TryGetValue(baseSymbolId, out symbol))
            {
                var sb = new StringBuilder();
                Markup.WriteSymbol(symbol, sb);
                writer.Write(sb.ToString());
            }
        }

        private static int CountItems(Tuple<string, IEnumerable<IGrouping<int, Reference>>> sameFileReferencesGroup)
        {
            int count = 0;

            foreach (var line in sameFileReferencesGroup.Item2)
            {
                foreach (var occurrence in line)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountItems(
            Tuple<string, IEnumerable<Tuple<string, IEnumerable<IGrouping<int, Reference>>>>> resultsInAssembly)
        {
            int count = 0;
            foreach (var file in resultsInAssembly.Item2)
            {
                count += CountItems(file);
            }

            return count;
        }

        private static int CountItems(
            Tuple<ReferenceKind, IEnumerable<Tuple<string, IEnumerable<Tuple<string, IEnumerable<IGrouping<int, Reference>>>>>>> results)
        {
            int count = 0;
            foreach (var item in results.Item2)
            {
                count += CountItems(item);
            }

            return count;
        }

        private static
            IEnumerable<Tuple<ReferenceKind,
                IEnumerable<Tuple<string,
                    IEnumerable<Tuple<string,
                        IEnumerable<IGrouping<int, Reference>>
                    >>
                >>
            >> CreateReferences(
            IEnumerable<Reference> list,
            out string referencedSymbolName)
        {
            referencedSymbolName = null;

            foreach (var reference in list)
            {
                if (referencedSymbolName == null &&
                   reference.ToSymbolName != "this" &&
                   reference.ToSymbolName != "base" &&
                   reference.ToSymbolName != "var" &&
                   reference.ToSymbolName != "UsingTask" &&
                   reference.ToSymbolName != "[")
                {
                    referencedSymbolName = reference.ToSymbolName;
                }
            }

            var result = list.GroupBy
            (
                r0 => r0.Kind,
                (kind, referencesOfSameKind) => Tuple.Create
                (
                    kind,
                    referencesOfSameKind.GroupBy
                    (
                        r1 => r1.FromAssemblyId,
                        (assemblyName, referencesInSameAssembly) => Tuple.Create
                        (
                            assemblyName,
                            referencesInSameAssembly.GroupBy
                            (
                                r2 => r2.FromLocalPath,
                                (filePath, referencesInSameFile) => Tuple.Create
                                (
                                    filePath,
                                    referencesInSameFile.GroupBy
                                    (
                                        r3 => r3.ReferenceLineNumber
                                    )
                                )
                            )
                        )
                    )
                )
            );

            return result;
        }

        private static void MergeOccurrences(System.IO.StreamWriter writer, IEnumerable<Reference> referencesOnTheSameLine)
        {
            var text = referencesOnTheSameLine.First().ReferenceLineText;
            referencesOnTheSameLine = referencesOnTheSameLine.OrderBy(r => r.ReferenceColumnStart);
            int current = 0;
            foreach (var occurrence in referencesOnTheSameLine)
            {
                if (occurrence.ReferenceColumnStart < 0 ||
                    occurrence.ReferenceColumnStart >= text.Length ||
                    occurrence.ReferenceColumnEnd <= occurrence.ReferenceColumnStart)
                {
                    string message = "occurrence.ReferenceColumnStart = " + occurrence.ReferenceColumnStart;
                    message += "\r\noccurrence.ReferenceColumnEnd = " + occurrence.ReferenceColumnEnd;
                    message += "\r\ntext = " + text;
                    Log.Exception("MergeOccurrences1: " + message);
                }

                if (occurrence.ReferenceColumnStart > current)
                {
                    if (current < 0 ||
                        current >= text.Length ||
                        occurrence.ReferenceColumnStart < current ||
                        occurrence.ReferenceColumnStart >= text.Length)
                    {
                        string message = "occurrence.ReferenceColumnStart = " + occurrence.ReferenceColumnStart;
                        message += "\r\noccurrence.ReferenceColumnEnd = " + occurrence.ReferenceColumnEnd;
                        message += "\r\ntext = " + text;
                        message += "\r\ncurrent = " + current;
                        Log.Exception("MergeOccurrences2: " + message);
                    }
                    else
                    {
                        Write(writer, text.Substring(current, occurrence.ReferenceColumnStart - current));
                    }
                }

                Write(writer, "<i>");
                Write(writer, text.Substring(occurrence.ReferenceColumnStart, occurrence.ReferenceColumnEnd - occurrence.ReferenceColumnStart));
                Write(writer, "</i>");
                current = occurrence.ReferenceColumnEnd;
            }

            if (current < text.Length)
            {
                Write(writer, text.Substring(current, text.Length - current));
            }
        }

        private static void Write(System.IO.StreamWriter sw, string text)
        {
            sw.Write(text);
        }

        private static void Write(System.IO.StreamWriter sw, string format, params object[] args)
        {
            sw.Write(string.Format(format, args));
        }

        private static void WriteLine(System.IO.StreamWriter sw, string text)
        {
            sw.WriteLine(text);
        }

        private static void WriteLine(System.IO.StreamWriter sw, string format, params object[] args)
        {
            sw.WriteLine(string.Format(format, args));
        }
    }
}
