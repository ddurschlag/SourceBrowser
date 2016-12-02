using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class ProjectFinalizer
    {
        private void BackpatchUnreferencedDeclarations(string referencesFolder)
        {
            string declarationMapFile = Path.Combine(ProjectDestinationFolder, Constants.DeclarationMap + ".txt");
            if (!File.Exists(declarationMapFile))
            {
                return;
            }

            Log.Write("Backpatching unreferenced declarations in " + this.AssemblyId);

            var symbolIDToListOfLocationsMap = ReadSymbolIDToListOfLocationsMap(declarationMapFile);

            ProjectGenerator.GenerateRedirectFile(
                this.SolutionFinalizer.SolutionDestinationFolder,
                this.ProjectDestinationFolder,
                symbolIDToListOfLocationsMap.ToDictionary(
                    kvp => kvp.Item1,
                    kvp => kvp.Item2.Select(t => t.FilePath.Replace('\\', '/'))));

            var locationsToPatch = new Dictionary<string, List<long>>();
            GetLocationsToPatch(referencesFolder, locationsToPatch, symbolIDToListOfLocationsMap);
            Patch(locationsToPatch);
        }

        private void GetLocationsToPatch(string referencesFolder, Dictionary<string, List<long>> locationsToPatch, SymbolIndex symbolIDToListOfLocationsMap)
        {
            foreach (var kvp in symbolIDToListOfLocationsMap)
            {
                var symbolId = kvp.Item1;
                var referencesFileForSymbol = Path.Combine(referencesFolder, symbolId + ".txt");
                if (!File.Exists(referencesFileForSymbol))
                {
                    foreach (var location in kvp.Item2)
                    {
                        if (location.Offset != 0)
                        {
                            var filePath = Path.Combine(ProjectDestinationFolder, location.FilePath + ".html");
                            AddLocationToPatch(locationsToPatch, filePath, location.Offset);
                        }
                    }
                }
            }
        }

        private static void Patch(Dictionary<string, List<long>> locationsToPatch)
        {
            byte[] zeroId = SymbolIdService.ZeroId;
            int zeroIdLength = zeroId.Length;
            Parallel.ForEach(locationsToPatch,
                new ParallelOptions { MaxDegreeOfParallelism = Configuration.Parallelism },
                kvp =>
                {
                    kvp.Value.Sort();

                    using (var stream = new FileStream(kvp.Key, FileMode.Open, FileAccess.ReadWrite))
                    {
                        foreach (var offset in kvp.Value)
                        {
                            stream.Seek(offset, SeekOrigin.Begin);
                            stream.Write(zeroId, 0, zeroIdLength);
                        }
                    }
                });
        }

        private SymbolIndex ReadSymbolIDToListOfLocationsMap(string declarationMapFile)
        {
            var result = new SymbolIndex();

            var lines = File.ReadAllLines(declarationMapFile);

            //File.Delete(declarationMapFile);

            string symbolId = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.StartsWith("="))
                {
                    symbolId = line.Substring(1);
                }
                else if (!string.IsNullOrWhiteSpace(line) && symbolId != null)
                {
                    result.Add(symbolId, SymbolLocation.Read(line));
                }
            }

            return result;
        }

        private void AddLocationToPatch(Dictionary<string, List<long>> locationsToPatch, string filePath, long offset)
        {
            List<long> offsets = null;
            if (!locationsToPatch.TryGetValue(filePath, out offsets))
            {
                offsets = new List<long>();
                locationsToPatch.Add(filePath, offsets);
            }

            offsets.Add(offset);
        }
    }
}
