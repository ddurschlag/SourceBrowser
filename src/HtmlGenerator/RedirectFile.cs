using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class RedirectFile
    {
        /// <summary>
        /// Since the only requirement on the ID strings we use in the a.html file
        /// is that there are no collisions (and even if there are, the failure
        /// would be rare and impact would be limited), we don't really need 16
        /// bytes per ID. Let's just store the first 8 bytes (I've actually calculated
        /// using MinimalUniquenessPreservingPrefixLength that 7 bytes are sufficient
        /// but let's add another byte to reduce the probability of future collisions)
        /// </summary>
        public static int SIGNIFICANT_ID_BYTES = 8;

        private IO.ProjectManager IOManager;
        private Common.SymbolIndex Index;

        public RedirectFile(IO.ProjectManager ioManager, Common.SymbolIndex si)
        {
            IOManager = ioManager;
            Index = si;
        }

        public void Generate()
        {
            using (var writer = IOManager.GetIDResolvingWriter(string.Empty))
            {
                Markup.WriteMetadataToSourceRedirectPrefix(writer);

                writer.WriteLine("redirectToNextLevelRedirectFile();");

                foreach (var map in FirstLetterIndices)
                {
                    new RedirectFile(IOManager, map.Item2).Generate(map.Item1);
                }

                Markup.WriteMetadataToSourceRedirectSuffix(writer);
            }
        }

        private void Generate(char c)
        {
            using (var writer = IOManager.GetIDResolvingWriter(c.ToString()))
            {
                Markup.WriteMetadataToSourceRedirectPrefix(writer);
                WriteMapping(writer);
                Markup.WriteMetadataToSourceRedirectSuffix(writer);
            }
        }

        private IEnumerable<Tuple<char, Common.SymbolIndex>> FirstLetterIndices
        {
            get
            {
                foreach (var g in Index.GroupBy(t => t.Item1[0]))
                {
                    var result = new Common.SymbolIndex();

                    foreach (var item in g)
                    {
                        result.Add(item.Item1, item.Item2);
                    }

                    yield return Tuple.Create(g.Key, result);
                }
            }
        }

        private void WriteMapping(System.IO.StreamWriter writer)
        {
            var files = FilePaths;
            var fileIndexLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            writer.WriteLine("var f = [");
            for (int i = 0; i < files.Length; i++)
            {
                fileIndexLookup.Add(files[i], i);
                writer.WriteLine("\"" + files[i] + "\",");
            }

            writer.WriteLine("];");

            writer.WriteLine("var m = new Object();");

            foreach (var kvp in Index)
            {
                string shortenedKey = GetShortenedKey(kvp.Item1);
                var filePaths = kvp.Item2;

                if (filePaths.Count() == 1)
                {
                    var value = filePaths.First();
                    writer.WriteLine("m[\"" + shortenedKey + "\"]=f[" + fileIndexLookup[GetPath(value)].ToString() + "];");
                }
                else
                {
                    writer.WriteLine("m[\"" + shortenedKey + "\"]=\"" + Constants.PartialResolvingFileName + "/" + kvp.Item1 + "\";");
                    IOManager.GeneratePartialTypeDisambiguationFile(kvp.Item1, filePaths.Select(GetPath));
                }
            }

            writer.WriteLine("redirect(m, {0});", SIGNIFICANT_ID_BYTES);
        }

        private static string GetPath(Common.SymbolLocation sl)
        {
            return sl.FilePath.Replace('\\', '/');
        }

        private string[] FilePaths
        {
            get
            {
                var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in Index)
                {
                    files.UnionWith(kvp.Item2.Select(GetPath));
                }

                var array = files.ToArray();
                Array.Sort(array);

                return array;
            }
        }

        private static string GetShortenedKey(string key)
        {
            var shortenedKey = key;
            if (shortenedKey.Length > SIGNIFICANT_ID_BYTES)
            {
                shortenedKey = shortenedKey.Substring(0, SIGNIFICANT_ID_BYTES);
            }

            // all the keys in this file start with the same prefix, no need to include it
            shortenedKey = shortenedKey.Substring(1);
            return shortenedKey;
        }
    }
}
