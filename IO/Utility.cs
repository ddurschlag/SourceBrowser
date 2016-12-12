using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.SourceBrowser.Common;

namespace Microsoft.SourceBrowser.IO
{
    public static class Utility
    {
        public static void SortLines(string filePath)
        {
            if (File.Exists(filePath))
            {
                var lines = File.ReadAllLines(filePath);
                Array.Sort(lines, StringComparer.OrdinalIgnoreCase);
                File.WriteAllLines(filePath, lines);
            }
        }

        public static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                throw new ArgumentException("Source directory doesn't exist:" + sourceDirectory);
            }

            sourceDirectory = sourceDirectory.TrimSlash();

            if (string.IsNullOrEmpty(destinationDirectory))
            {
                throw new ArgumentNullException("destinationDirectory");
            }

            destinationDirectory = destinationDirectory.TrimSlash();

            var files = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relative = file.Substring(sourceDirectory.Length + 1);
                var destination = Path.Combine(destinationDirectory, relative);
                CopyFile(file, destination);
            }
        }

        public static void CopyFile(string sourceFilePath, string destinationFilePath, bool overwrite = false)
        {
            if (!File.Exists(sourceFilePath))
            {
                return;
            }

            if (!overwrite && File.Exists(destinationFilePath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(destinationFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(sourceFilePath, destinationFilePath, overwrite);
            File.SetAttributes(destinationFilePath, File.GetAttributes(destinationFilePath) & ~FileAttributes.ReadOnly);
        }

        public static void DeployFilesToRoot(
            string destinationFolder,
            bool emitAssemblyList,
            IEnumerable<string> federatedServers)
        {
            WriteReferencesNotFoundFile(destinationFolder);

            string basePath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            string sourcePath = Path.Combine(basePath, @"Web");
            if (!Directory.Exists(sourcePath))
            {
                return;
            }

            sourcePath = Path.GetFullPath(sourcePath);
            CopyDirectory(sourcePath, destinationFolder);

            StampOverviewHtmlWithDate(destinationFolder);
            if (emitAssemblyList)
            {
                ToggleSolutionExplorerOff(destinationFolder);
            }

            SetExternalUrlMap(destinationFolder, federatedServers);

            DeployBin(basePath, destinationFolder);
        }

        private static string zeroFileName = "0000000000.html";

        private static void WriteReferencesNotFoundFile(string folder)
        {
            string html = @"<!DOCTYPE html>
<html><head><link rel=""stylesheet"" href=""styles.css""/></head>
<body><div class=""rH"">No references found</div></body></html>";
            string filePath = Path.Combine(folder, zeroFileName);
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, html, Encoding.UTF8);
            }
        }

        private static void StampOverviewHtmlWithDate(string destinationFolder)
        {
            var overviewHtml = Path.Combine(destinationFolder, "overview.html");
            if (File.Exists(overviewHtml))
            {
                var text = File.ReadAllText(overviewHtml);
                text = StampOverviewHtmlText(text);
                File.WriteAllText(overviewHtml, text);
            }
        }

        private static string StampOverviewHtmlText(string text)
        {
            text = text.Replace("$(Date)", DateTime.Today.ToString("MMMM d", CultureInfo.InvariantCulture));
            return text;
        }

        private static void ToggleSolutionExplorerOff(string destinationFolder)
        {
            var scriptsJs = Path.Combine(destinationFolder, "scripts.js");
            if (File.Exists(scriptsJs))
            {
                var text = File.ReadAllText(scriptsJs);
                text = text.Replace("/*USE_SOLUTION_EXPLORER*/true/*USE_SOLUTION_EXPLORER*/", "false");
                File.WriteAllText(scriptsJs, text);
            }
        }

        private static void SetExternalUrlMap(string destinationFolder, IEnumerable<string> federatedServers)
        {
            var scriptsJs = Path.Combine(destinationFolder, "scripts.js");
            if (File.Exists(scriptsJs))
            {
                var sb = new StringBuilder();
                foreach (var server in federatedServers)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(",");
                    }

                    sb.Append("\"");
                    sb.Append(server);
                    sb.Append("\"");
                }

                if (sb.Length > 0)
                {
                    var text = File.ReadAllText(scriptsJs);
                    text = Regex.Replace(text, @"/\*EXTERNAL_URL_MAP\*/.*/\*EXTERNAL_URL_MAP\*/", sb.ToString());
                    File.WriteAllText(scriptsJs, text);
                }
            }
        }

        private static void DeployBin(string sourcePath, string destinationFolder)
        {
            var files = new[]
            {
                "Microsoft.SourceBrowser.IO.dll",
                "Microsoft.SourceBrowser.Common.dll",
                "Microsoft.SourceBrowser.SourceIndexServer.dll",
                "Microsoft.Web.Infrastructure.dll",
                "Newtonsoft.Json.dll",
                "System.Net.Http.Formatting.dll",
                "System.Web.Helpers.dll",
                "System.Web.Http.dll",
                "System.Web.Http.WebHost.dll",
                "System.Web.Mvc.dll",
                "System.Web.Razor.dll",
                "System.Web.WebPages.dll",
                "System.Web.WebPages.Deployment.dll",
                "System.Web.WebPages.Razor.dll",
            };

            foreach (var file in files)
            {
                CopyFile(
                    Path.Combine(sourcePath, file),
                    Path.Combine(destinationFolder, "bin", file));
            }
        }
    }
}
