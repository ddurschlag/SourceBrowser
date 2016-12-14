using System;
using System.Collections.Generic;
using Path = System.IO.Path;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.SourceBrowser.Common;
using Microsoft.SourceBrowser.Common.Entity;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class ProjectGenerator
    {
        public readonly Dictionary<string, Dictionary<string, List<Reference>>> ReferencesByTargetAssemblyAndSymbolId =
            new Dictionary<string, Dictionary<string, List<Reference>>>();

        public readonly Dictionary<string, Dictionary<string, List<Reference>>> ReferencesBySourceAssemblyAndSymbolId =
            new Dictionary<string, Dictionary<string, List<Reference>>>();

        public IEnumerable<string> UsedReferences { get; private set; }

        public void AddLegacyReferenceForMSBuild(
            string documentDestinationPath,
            string lineText,
            int referenceStartOnLine,
            int referenceLength,
            int lineNumber,
            string fromAssemblyName,
            string toAssemblyName,
            ISymbol symbol,
            string symbolId,
            ReferenceKind kind)
        {
            AddReference(ref lineText, ref referenceStartOnLine, referenceLength, lineNumber, fromAssemblyName, toAssemblyName, symbol, symbolId, kind, documentDestinationPath);
        }

        public void AddReference(
            IO.Destination documentDestination,
            SourceText referenceText,
            string destinationAssemblyName,
            ISymbol symbol,
            string symbolId,
            int startPosition,
            int endPosition,
            ReferenceKind kind)
        {
            string referenceString = referenceText.ToString(TextSpan.FromBounds(startPosition, endPosition));
            if (symbol is INamedTypeSymbol && (referenceString == "this" || referenceString == "base"))
            {
                // Don't count "this" or "base" expressions that bind to this type as references
                return;
            }

            var line = referenceText.Lines.GetLineFromPosition(startPosition);
            int start = referenceText.Lines.GetLinePosition(startPosition).Character;
            int end = start + endPosition - startPosition;
            int lineNumber = line.LineNumber + 1;
            string lineText = line.ToString();

            AddReference(
                documentDestination,
                lineText,
                start,
                referenceString.Length,
                lineNumber,
                AssemblyName,
                destinationAssemblyName,
                symbol,
                symbolId,
                kind);
        }

        public void AddReference(
            IO.Destination documentDestination,
            string lineText,
            int referenceStartOnLine,
            int referenceLength,
            int lineNumber,
            string fromAssemblyName,
            string toAssemblyName,
            ISymbol symbol,
            string symbolId,
            ReferenceKind kind)
        {
            var documentDestinationPath = Path.Combine(documentDestination.Folders.Concat(new[] { documentDestination.FileName }).ToArray());// IOManager.GetLegacyDocumentDestinationPath(documentDestination);
            AddReference(ref lineText, ref referenceStartOnLine, referenceLength, lineNumber, fromAssemblyName, toAssemblyName, symbol, symbolId, kind, documentDestinationPath);
        }

        private void AddReference(ref string lineText, ref int referenceStartOnLine, int referenceLength, int lineNumber, string fromAssemblyName, string toAssemblyName, ISymbol symbol, string symbolId, ReferenceKind kind, string documentDestinationPath)
        {
            string localPath = documentDestinationPath;

            int referenceEndOnLine = referenceStartOnLine + referenceLength;

            lineText = Markup.HtmlEscape(lineText, ref referenceStartOnLine, ref referenceEndOnLine);

            string symbolName = GetSymbolName(symbol, symbolId);

            var reference = new Reference()
            {
                ToAssemblyId = toAssemblyName,
                ToSymbolId = symbolId,
                ToSymbolName = symbolName,
                FromAssemblyId = fromAssemblyName,
                FromLocalPath = localPath,
                ReferenceLineText = lineText,
                ReferenceLineNumber = lineNumber,
                ReferenceColumnStart = referenceStartOnLine,
                ReferenceColumnEnd = referenceEndOnLine,
                Kind = kind
            };

            if (referenceStartOnLine < 0 ||
                referenceStartOnLine >= referenceEndOnLine ||
                referenceEndOnLine > lineText.Length)
            {
                Log.Exception(
                    string.Format("AddReference: start = {0}, end = {1}, lineText = {2}, documentDestinationPath = {3}",
                    referenceStartOnLine,
                    referenceEndOnLine,
                    lineText,
                    documentDestinationPath));
            }

            string linkRelativePath = GetLinkRelativePath(reference);

            reference.Url = linkRelativePath;

            AddReference(reference, GetReferencesToAssembly(reference.ToAssemblyId));
            AddReference(reference, GetReferencesFromAssembly(reference.FromAssemblyId));
        }

        private static void AddReference(Reference reference, Dictionary<string, List<Reference>> referencesToAssembly)
        {
            List<Reference> referencesToSymbol = GetReferencesToSymbol(reference, referencesToAssembly);
            lock (referencesToSymbol)
            {
                referencesToSymbol.Add(reference);
            }
        }

        private static List<Reference> GetReferencesToSymbol(Reference reference, Dictionary<string, List<Reference>> referencesToAssembly)
        {
            List<Reference> referencesToSymbol;
            lock (referencesToAssembly)
            {
                if (!referencesToAssembly.TryGetValue(reference.ToSymbolId, out referencesToSymbol))
                {
                    referencesToSymbol = new List<Reference>();
                    referencesToAssembly.Add(reference.ToSymbolId, referencesToSymbol);
                }
            }

            return referencesToSymbol;
        }

        private Dictionary<string, List<Reference>> GetReferencesToAssembly(string assembly)
        {
            Dictionary<string, List<Reference>> referencesToAssembly;
            lock (ReferencesByTargetAssemblyAndSymbolId)
            {
                if (!ReferencesByTargetAssemblyAndSymbolId.TryGetValue(assembly, out referencesToAssembly))
                {
                    referencesToAssembly = new Dictionary<string, List<Reference>>(StringComparer.OrdinalIgnoreCase);
                    ReferencesByTargetAssemblyAndSymbolId.Add(assembly, referencesToAssembly);
                }
            }

            return referencesToAssembly;
        }

        private Dictionary<string, List<Reference>> GetReferencesFromAssembly(string assembly)
        {
            Dictionary<string, List<Reference>> referencesFromAssembly;
            lock (ReferencesBySourceAssemblyAndSymbolId)
            {
                if (!ReferencesBySourceAssemblyAndSymbolId.TryGetValue(assembly, out referencesFromAssembly))
                {
                    referencesFromAssembly = new Dictionary<string, List<Reference>>(StringComparer.OrdinalIgnoreCase);
                    ReferencesBySourceAssemblyAndSymbolId.Add(assembly, referencesFromAssembly);
                }
            }

            return referencesFromAssembly;
        }

        private static string GetLinkRelativePath(Reference reference)
        {
            string linkRelativePath = reference.FromLocalPath.Replace('\\', '/') + ".html#" + reference.ReferenceLineNumber;
            if (reference.ToAssemblyId == reference.FromAssemblyId)
            {
                linkRelativePath = "../" + linkRelativePath;
            }
            else
            {
                linkRelativePath = "../../" + reference.FromAssemblyId + "/" + linkRelativePath;
            }

            return linkRelativePath;
        }

        private static string GetSymbolName(ISymbol symbol, string symbolId)
        {
            string symbolName = null;
            if (symbol != null)
            {
                symbolName = SymbolIdService.GetName(symbol);
                if (symbolName == ".ctor")
                {
                    symbolName = SymbolIdService.GetName(symbol.ContainingType) + " .ctor";
                }
            }
            else
            {
                symbolName = symbolId;
            }

            return symbolName;
        }

        private void GenerateUsedReferencedAssemblyList()
        {
            this.UsedReferences = ReferencesByTargetAssemblyAndSymbolId
                .Select(r => r.Key)
                .Where(a =>
                    a != AssemblyName &&
                    a != Constants.MSBuildPropertiesAssembly &&
                    a != Constants.MSBuildItemsAssembly &&
                    a != Constants.MSBuildTargetsAssembly &&
                    a != Constants.MSBuildTasksAssembly &&
                    a != Constants.GuidAssembly);

            //todo: Log this usefully, if needed?
            //Log.Write("Used Assemblies:", ConsoleColor.DarkGray);
            //    foreach ( var s in UsedReferences )
            //    {
            //Log.Write(s, ConsoleColor.DarkGray);
            //    }
        }

        private void GenerateReferencedAssemblyList()
        {
            Log.Write("Referenced assembly list...");
            //var index = Path.Combine(ProjectDestinationFolder, Constants.ReferencedAssemblyList + ".txt");
            var list = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var projectReference in Project.ProjectReferences.OrderBy(p => Project.Solution.GetProject(p.ProjectId).AssemblyName))
            {
                list.Add(Project.Solution.GetProject(projectReference.ProjectId).AssemblyName);
            }

            foreach (var metadataReference in Project.MetadataReferences.OrderBy(m => Path.GetFileNameWithoutExtension(m.Display)))
            {
                list.Add(Path.GetFileNameWithoutExtension(metadataReference.Display));
            }

            //todo: Log this usefully, if needed?
            //Log.Write("Referenced Assemblies:", ConsoleColor.DarkGray);
            //    foreach ( var s in list )
            //    {
            //Log.Write(s, ConsoleColor.DarkGray);
            //    }
        }

        public void GenerateReferencesDataFiles()
        {
            Log.Write("References data files...", ConsoleColor.White);

            Log.Write("All from assemblies: " + string.Join(", ", ReferencesByTargetAssemblyAndSymbolId.SelectMany(kvp => kvp.Value.SelectMany(kvp2 => kvp2.Value.Select(r => r.FromAssemblyId))).Distinct()), ConsoleColor.Cyan);

            SolutionGenerator.IOManager.AppendReferences(ReferencesBySourceAssemblyAndSymbolId);
        }
    }
}
