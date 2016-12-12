using System;
using System.Collections.Generic;
using Path = System.IO.Path;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class ProjectFinalizer
    {
        private void BackpatchUnreferencedDeclarations()
        {
            Log.Write("Backpatching unreferenced declarations in " + this.AssemblyId);

            var symbolIDToListOfLocationsMap = IOManager.GetDeclarationMap();

            new RedirectFile(IOManager, symbolIDToListOfLocationsMap).Generate();

            var locationsToPatch = new Dictionary<IO.Destination, List<long>>();
            GetLocationsToPatch(locationsToPatch, symbolIDToListOfLocationsMap);
            Patch(locationsToPatch);
        }

        private void GetLocationsToPatch(Dictionary<IO.Destination, List<long>> locationsToPatch, SymbolIndex symbolIDToListOfLocationsMap)
        {
            foreach (var kvp in symbolIDToListOfLocationsMap)
            {
                if (!IOManager.ReferencesExists(kvp.Item1))
                {
                    foreach (var location in kvp.Item2)
                    {
                        if (location.Offset != 0)
                        {
                            AddLocationToPatch(locationsToPatch, new IO.Destination(location.FilePath), location.Offset);
                        }
                    }
                }
            }
        }

        private void Patch(Dictionary<IO.Destination, List<long>> locationsToPatch)
        {
            Parallel.ForEach(locationsToPatch,
                new ParallelOptions { MaxDegreeOfParallelism = Configuration.Parallelism },
                kvp =>
                {
                    kvp.Value.Sort();

                    IOManager.Patch(kvp.Key, SymbolIdService.ZeroId, kvp.Value);
                });
        }

        private void AddLocationToPatch(Dictionary<IO.Destination, List<long>> locationsToPatch, IO.Destination filePath, long offset)
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
