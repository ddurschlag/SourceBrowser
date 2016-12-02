using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Microsoft.SourceBrowser.IO
{
    public class IOManager
    {
        protected static string MakeRelativeToFile(string filePath, string relativeToPath)
        {
            relativeToPath = Path.GetDirectoryName(relativeToPath);
            string result = MakeRelativeToFolder(filePath, relativeToPath);
            return result;
        }

        /// <summary>
        /// Returns a path to <paramref name="filePath"/> if you start in folder <paramref name="relativeToPath"/>.
        /// </summary>
        /// <param name="filePath">C:\A\B\1.txt</param>
        /// <param name="relativeToPath">C:\C\D</param>
        /// <returns>..\..\A\B\1.txt</returns>
        protected static string MakeRelativeToFolder(string filePath, string relativeToPath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (string.IsNullOrEmpty(relativeToPath))
            {
                throw new ArgumentNullException(nameof(relativeToPath));
            }

            // the file is on a different drive
            if (filePath[0] != relativeToPath[0])
            {
                // better than crashing
                return Path.GetFileName(filePath);
            }

            if (relativeToPath.EndsWith("\\"))
            {
                relativeToPath = relativeToPath.TrimEnd('\\');
            }

            StringBuilder result = new StringBuilder();
            while (!EnsureTrailingSlash(filePath).StartsWith(EnsureTrailingSlash(relativeToPath), StringComparison.OrdinalIgnoreCase))
            {
                result.Append(@"..\");
                relativeToPath = Path.GetDirectoryName(relativeToPath);
            }

            if (filePath.Length > relativeToPath.Length)
            {
                filePath = filePath.Substring(relativeToPath.Length);
                if (filePath.StartsWith("\\"))
                {
                    filePath = filePath.Substring(1);
                }

                result.Append(filePath);
            }

            return result.ToString();
        }

        protected static string EnsureTrailingSlash(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (!path.EndsWith("\\"))
            {
                path += "\\";
            }

            return path;
        }
    }
}
