using System;
using Path = System.IO.Path;
using Microsoft.CodeAnalysis;
using Microsoft.SourceBrowser.Common;
using Folder = Microsoft.SourceBrowser.HtmlGenerator.Folder<Microsoft.CodeAnalysis.Project>;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class SolutionFinalizer
    {
        private void WriteSolutionExplorer(Folder root = null)
        {
            if (root == null)
            {
                return;
            }

            Sort(root);

            using (var writer = IOManager.GetSolutionExplorerWriter())
            {
                Log.Write("Solution Explorer...");
                Markup.WriteSolutionExplorerPrefix(writer);
                WriteFolder(root, writer);
                Markup.WriteSolutionExplorerSuffix(writer);
            }
        }

        private void Sort(Folder<Project> root, Comparison<string> customRootSorter = null)
        {
            if (Configuration.FlattenSolutionExplorer)
            {
                if (customRootSorter == null)
                {
                    customRootSorter = (l, r) => StringComparer.OrdinalIgnoreCase.Compare(l, r);
                }

                root.Sort((l, r) => customRootSorter(l.AssemblyName, r.AssemblyName));
            }
            else
            {
                root.Sort((l, r) => StringComparer.OrdinalIgnoreCase.Compare(l.Name, r.Name));
            }
        }

        private void WriteFolder(Folder folder, System.IO.StreamWriter writer)
        {
            if (folder.Folders != null)
            {
                foreach (var subfolder in folder.Folders.Values)
                {
                    writer.WriteLine(@"<div class=""folderTitle"">{0}</div><div class=""folder"">", subfolder.Name);
                    WriteFolder(subfolder, writer);
                    writer.WriteLine("</div>");
                }
            }

            if (folder.Items != null)
            {
                foreach (var project in folder.Items)
                {
                    WriteProject(project.AssemblyName, writer);
                }
            }
        }

        private void WriteProject(string assemblyName, System.IO.StreamWriter writer)
        {
            var text = string.Join(Environment.NewLine, IOManager.GetProjectManager(assemblyName).ReadProjectExplorer());
            var projectExplorerText = GetProjectExplorerText(text, assemblyName);
            if (!string.IsNullOrEmpty(projectExplorerText))
            {
                writer.WriteLine(projectExplorerText);
            }
        }

        private string GetProjectExplorerText(string text, string assemblyName)
        {
            var startText = "<div id=\"rootFolder\"";
            var start = text.IndexOf(startText) + startText.Length;
            var end = text.IndexOf("<script>");
            text = text.Substring(start, end - start);
            text = "<div" + text;
            text = text.Replace(@"</div><div>", string.Format("</div><div class=\"folder\" data-assembly=\"{0}\">", assemblyName));
            text = text.Replace("projectCS", "projectCSInSolution");
            text = text.Replace("projectVB", "projectVBInSolution");

            var projectInfoStart = text.IndexOf("<p class=\"projectInfo");
            if (projectInfoStart != -1)
            {
                var projectInfoEnd = text.IndexOf("</p>", projectInfoStart) + 4;
                if (projectInfoEnd != -1)
                {
                    text = text.Remove(projectInfoStart, projectInfoEnd - projectInfoStart);
                }
            }

            return text;
        }
    }
}
