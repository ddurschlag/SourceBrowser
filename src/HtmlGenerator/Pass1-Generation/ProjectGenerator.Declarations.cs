using System;
using System.Collections.Generic;
using Path = System.IO.Path;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class ProjectGenerator
    {
        /// <summary>
        /// Two things are happening to declared symbols at the time we write the definition to HTML stream.
        /// First, the symbol is added to DeclaredSymbols (to be later written to D.txt for each project).
        /// Second, the redirect from SymbolId to local file path is added to A.txt (metadata to source redirect).
        /// </summary>
        /// <param name="declaredSymbol">Declared symbol. Can be null for files.</param>
        /// <param name="symbolId">ID of the declared symbol.</param>
        /// <param name="documentRelativeFilePath">Project-local path to the file where the symbol is defined.</param>
        /// <param name="positionInFile">Exact byte position in the HTML file stream being generated where the 16-char
        /// symbol ID starts, to later be back patched to 0000000000000000 if the symbol has no references.</param>
        public void AddDeclaredSymbol(
            ISymbol declaredSymbol,
            string symbolId,
            string documentRelativeFilePath,
            long positionInFile)
        {
            if (declaredSymbol != null)
            {
                if (declaredSymbol.Kind == SymbolKind.Local ||
                    declaredSymbol.Kind == SymbolKind.Parameter ||
                    declaredSymbol.Kind == SymbolKind.TypeParameter)
                {
                    return;
                }

                // We care about indexing even private symbols for NavigateTo
                lock (DeclaredSymbols)
                {
                    var declaredSymbolName = declaredSymbol.Name;
                    if (declaredSymbolName != ".ctor" &&
                        declaredSymbolName != ".cctor" &&
                        !DeclaredSymbols.ContainsKey(declaredSymbol))
                    {
                        DeclaredSymbols.Add(declaredSymbol, symbolId);
                    }
                }
            }

            AddDeclaredSymbolToRedirectMap(SymbolIDToListOfLocationsMap, symbolId, documentRelativeFilePath, positionInFile);
        }

        public static void AddDeclaredSymbolToRedirectMap(
            SymbolIndex symbolIDToListOfLocationsMap,
            string symbolId,
            string documentRelativeFilePath,
            long positionInFile)
        {
            lock (symbolIDToListOfLocationsMap)
            {
                symbolIDToListOfLocationsMap.Add(symbolId, new SymbolLocation(documentRelativeFilePath, positionInFile));
            }
        }

        private void GenerateDeclarations(bool overwrite = false)
        {
            Log.Write("Declarations...");

            IOManager.WriteDeclaredSymbols(
                GetDeclaredSymbolLines(DeclaredSymbols).Concat(GetOtherFileLines(OtherFiles)),
                overwrite
            );
        }

        private static IEnumerable<Common.Entity.DeclaredSymbolInfo> GetOtherFileLines(IEnumerable<string> otherFiles)
        {
            if (otherFiles != null)
            {
                foreach (var document in otherFiles.OrderBy(d => d))
                {
                    yield return new Utilities.DeclaredSymbolInfoFactory().Manufacture(
                        SymbolIdService.GetId(document),
                        Path.GetFileName(document),
                        "file",
                        Markup.EscapeSemicolons(document),
                        Serialization.GetIconForExtension(document)
                    );


                    //yield return string.Join(";",
                    //    Path.GetFileName(document),
                    //    SymbolIdService.GetId(document),
                    //    "file",
                    //    Markup.EscapeSemicolons(document),
                    //    Serialization.GetIconForExtension(document));
                }
            }
        }

        private IEnumerable<Common.Entity.DeclaredSymbolInfo> GetDeclaredSymbolLines(IEnumerable<KeyValuePair<ISymbol, string>> declaredSymbols)
        {
            if (declaredSymbols != null)
            {
                foreach (var declaredSymbol in declaredSymbols
                    .OrderBy(s => SymbolIdService.GetName(s.Key))
                    .ThenBy(s => SymbolIdService.GetId(s.Key)))
                {
                    if (declaredSymbol.Value != SymbolIdService.GetId(declaredSymbol.Key))
                    {
                        Console.WriteLine("Inconsistency:");
                        Console.WriteLine(declaredSymbol.Value);
                        Console.WriteLine(SymbolIdService.GetId(declaredSymbol.Key));
                        throw new Exception("INCONSISTENCY!");
                    }

                    yield return new Utilities.DeclaredSymbolInfoFactory().Manufacture(
                        SymbolIdService.GetId(declaredSymbol.Key),
                        SymbolIdService.GetName(declaredSymbol.Key),
                        SymbolKindText.GetSymbolKind(declaredSymbol.Key),
                        Markup.EscapeSemicolons(SymbolIdService.GetDisplayString(declaredSymbol.Key)),
                        SymbolIdService.GetGlyphNumber(declaredSymbol.Key).ToString()
                    );

                    //yield return string.Join(";",
                    //    SymbolIdService.GetName(declaredSymbol.Key), // symbol name
                    //    declaredSymbol.Value, // 8-byte symbol ID
                    //    SymbolKindText.GetSymbolKind(declaredSymbol.Key), // kind (e.g. "class")
                    //    Markup.EscapeSemicolons(SymbolIdService.GetDisplayString(declaredSymbol.Key)), // symbol full name and signature
                    //    SymbolIdService.GetGlyphNumber(declaredSymbol.Key)); // icon number
                }
            }
        }

        public void GenerateSymbolIDToListOfDeclarationLocationsMap(
            SymbolIndex symbolIDToListOfLocationsMap,
            bool overwrite = false)
        {
            Log.Write("Symbol ID to list of locations map...");

            IOManager.WriteDeclarationsMap(symbolIDToListOfLocationsMap, overwrite);
        }

        public void AddBaseMember(ISymbol member, ISymbol baseMember)
        {
            lock (this.BaseMembers)
            {
                this.BaseMembers.Add(member, baseMember);
            }
        }

        public void GenerateBaseMembers()
        {
            Log.Write("Base members...");

            IOManager.CreateOutgoingReferencesDirectory();

            lock (this.BaseMembers)
            {
                var lines = new List<string>(this.BaseMembers.Count);
                foreach (var kvp in this.BaseMembers.OrderBy(b => SymbolIdService.GetId(b.Key)))
                {
                    var fromMemberId = SymbolIdService.GetId(kvp.Key);
                    var line =
                        fromMemberId + ";" +
                        SymbolIdService.GetAssemblyId(kvp.Value.ContainingAssembly) + ";" +
                        SymbolIdService.GetId(kvp.Value);
                    lines.Add(line);

                    // just make sure the references file for this symbol exists, so that even if symbols
                    // that aren't referenced anywhere get a reference file with a base member link if there
                    // is a base member for the symbol
                    IOManager.EnsureReferencesFile(fromMemberId);
                }

                IOManager.WriteBaseMembers(lines);
            }
        }

        public void AddImplementedInterfaceMember(ISymbol implementationMember, ISymbol interfaceMember)
        {
            if (implementationMember == null)
            {
                throw new ArgumentNullException("implementationMember");
            }

            if (interfaceMember == null)
            {
                throw new ArgumentNullException("interfaceMember");
            }

            lock (this.ImplementedInterfaceMembers)
            {
                this.ImplementedInterfaceMembers.Add(implementationMember, interfaceMember);
            }
        }

        public void GenerateImplementedInterfaceMembers()
        {
            Log.Write("Implemented interface members...");

            IOManager.CreateOutgoingReferencesDirectory();

            lock (this.ImplementedInterfaceMembers)
            {
                var lines = new List<string>(this.ImplementedInterfaceMembers.Count);
                foreach (var kvp in this.ImplementedInterfaceMembers.OrderBy(kvp => SymbolIdService.GetId(kvp.Key)))
                {
                    var fromMemberId = SymbolIdService.GetId(kvp.Key);

                    foreach (var implementedInterfaceMember in kvp.Value.OrderBy(s => SymbolIdService.GetId(s)))
                    {
                        var line =
                            fromMemberId + ";" +
                            SymbolIdService.GetAssemblyId(implementedInterfaceMember.ContainingAssembly) + ";" +
                            SymbolIdService.GetId(implementedInterfaceMember);
                        lines.Add(line);
                    }

                    // just make sure the references file for this symbol exists, so that even if symbols
                    // that aren't referenced anywhere get a reference file with a base member link if there
                    // is a base member for the symbol
                    IOManager.EnsureReferencesFile(fromMemberId);
                }

                IOManager.WriteImplementedInterfaceMembers(lines);
            }
        }
    }
}
