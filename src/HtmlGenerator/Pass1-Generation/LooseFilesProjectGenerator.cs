using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SourceBrowser.Common;
using Microsoft.CodeAnalysis;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class LooseFilesProjectGenerator
    {
        public List<string> OtherFiles { get; set; }
        public IO.ProjectManager IOManager { get; private set; }
        public SymbolIndex SymbolIDToListOfLocationsMap { get; private set; }

        public LooseFilesProjectGenerator(IO.ProjectManager ioManager)
        {
            this.SymbolIDToListOfLocationsMap = new SymbolIndex();
            this.OtherFiles = new List<string>();
            IOManager = ioManager;
            IOManager.CreateOutgoingReferencesDirectory();
        }

        public void Generate()
        {
            AddHtmlFilesToRedirectMap();
            GenerateDeclarations(true);
            GenerateSymbolIDToListOfDeclarationLocationsMap(
                SymbolIDToListOfLocationsMap,
                true);
        }

        private void GenerateSymbolIDToListOfDeclarationLocationsMap(
            SymbolIndex symbolIDToListOfLocationsMap,
            bool overwrite = false)
        {
            Log.Write("Symbol ID to list of locations map...");

            IOManager.WriteDeclarationsMap(symbolIDToListOfLocationsMap, overwrite);
        }


        private void GenerateDeclarations(bool overwrite = false)
        {
            Log.Write("Declarations...");

            IOManager.WriteDeclaredSymbols(
                GetOtherFileLines(OtherFiles),
                overwrite
            );
        }

        private static IEnumerable<Common.Entity.DeclaredSymbolInfo> GetOtherFileLines(IEnumerable<string> otherFiles)
        {
            if (otherFiles != null)
            {
                return otherFiles.OrderBy(d => d).Select(new Utilities.DeclaredSymbolInfoFactory().Manufacture);
            }
            return Enumerable.Empty<Common.Entity.DeclaredSymbolInfo>();
        }

        private void AddHtmlFilesToRedirectMap()
        {
            foreach (var file in IOManager.GetHtmlOutputFiles())
            {
                //todo: Pull into ProjectManager
                var relativePath = file.Substring(IOManager.ProjectDestinationFolder.Length + 1).Replace('\\', '/');
                relativePath = relativePath.Substring(0, relativePath.Length - 5); // strip .html
                if (!RedirectFileNames.Contains(relativePath))
                {
                    lock (SymbolIDToListOfLocationsMap)
                    {
                        SymbolIDToListOfLocationsMap.Add(
                            SymbolIdService.GetId(relativePath),
                            relativePath,
                            0L
                        );
                    }
                    OtherFiles.Add(relativePath);
                }
            }
        }
        private static HashSet<string> RedirectFileNames = new HashSet<string>
        {
            "A",
            "A1",
            "A2",
            "A3",
            "A4",
            "A5",
            "A6",
            "A7",
            "A8",
            "A9",
            "Aa",
            "Ab",
            "Ac",
            "Ad",
            "Ae",
            "Af",
        };
    }
}
