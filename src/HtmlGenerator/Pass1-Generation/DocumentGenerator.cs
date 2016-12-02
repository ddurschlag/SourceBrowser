using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.SourceBrowser.Common;
using StreamWriter = System.IO.StreamWriter;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class DocumentGenerator
    {
        public ProjectGenerator projectGenerator;
        public Document Document;
        public IO.Destination documentDestination;
        public string relativePathToRoot;
        public string documentRelativeFilePathWithoutHtmlExtension;

        private Classification classifier;

        public SourceText Text;
        public SyntaxNode Root;
        public SemanticModel SemanticModel;
        public HashSet<ISymbol> DeclaredSymbols;
        public object SemanticFactsService;
        public object SyntaxFactsService;
        private Func<SemanticModel, SyntaxNode, CancellationToken, bool> isWrittenToDelegate;
        private Func<SyntaxToken, SyntaxNode> getBindableParentDelegate;

        public DocumentGenerator(
            ProjectGenerator projectGenerator,
            Document document)
        {
            this.projectGenerator = projectGenerator;
            this.Document = document;
        }

        public async Task Generate()
        {
            if (Configuration.CalculateRoslynSemantics)
            {
                this.Text = await Document.GetTextAsync();
                this.Root = await Document.GetSyntaxRootAsync();
                this.SemanticModel = await Document.GetSemanticModelAsync();
                this.SemanticFactsService = WorkspaceHacks.GetSemanticFactsService(this.Document);
                this.SyntaxFactsService = WorkspaceHacks.GetSyntaxFactsService(this.Document);

                var semanticFactsServiceType = SemanticFactsService.GetType();
                var isWrittenTo = semanticFactsServiceType.GetMethod("IsWrittenTo");
                this.isWrittenToDelegate = (Func<SemanticModel, SyntaxNode, CancellationToken, bool>)
                    Delegate.CreateDelegate(typeof(Func<SemanticModel, SyntaxNode, CancellationToken, bool>), SemanticFactsService, isWrittenTo);

                var syntaxFactsServiceType = SyntaxFactsService.GetType();
                var getBindableParent = syntaxFactsServiceType.GetMethod("GetBindableParent");
                this.getBindableParentDelegate = (Func<SyntaxToken, SyntaxNode>)
                    Delegate.CreateDelegate(typeof(Func<SyntaxToken, SyntaxNode>), SyntaxFactsService, getBindableParent);

                this.DeclaredSymbols = new HashSet<ISymbol>();

                Interlocked.Increment(ref projectGenerator.DocumentCount);
                Interlocked.Add(ref projectGenerator.LinesOfCode, Text.Lines.Count);
                Interlocked.Add(ref projectGenerator.BytesOfCode, Text.Length);
            }

            CalculateDocumentDestinationPath();
            CalculateRelativePathToRoot();

            // add the file itself as a "declared symbol", so that clicking on document in search
            // results redirects to the document
            ProjectGenerator.AddDeclaredSymbolToRedirectMap(
                this.projectGenerator.SymbolIDToListOfLocationsMap,
                SymbolIdService.GetId(this.Document),
                documentRelativeFilePathWithoutHtmlExtension,
                0);

            if (IOManager.DestinationExists(documentDestination))
            {
                // someone already generated this file, likely a shared linked file from elsewhere
                return;
            }

            this.classifier = new Classification();

            Log.Write(documentDestination.ToString());

            try
            {
                IOManager.CreateDirectory(documentDestination);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "Couldn't create destination directory", false);
                return;
            }

            using (var streamWriter = IOManager.GetWriter(documentDestination))
            {
                await GenerateHtml(streamWriter);
            }
        }

        private void CalculateDocumentDestinationPath()
        {
            documentRelativeFilePathWithoutHtmlExtension = Paths.GetRelativeFilePathInProject(Document);
            documentDestination = new IO.Destination(Document.Folders.ToArray(), Document.FilePath != null ? System.IO.Path.GetFileName(Document.FilePath) : Document.Name);
        }

        private void CalculateRelativePathToRoot()
        {
            this.relativePathToRoot = IOManager.GetPathToSolutionRoot(documentDestination);
        }

        private async Task GenerateHtml(StreamWriter writer)
        {
            var title = Document.Name;
            var lineCount = Text.Lines.Count;

            // if the document is very long, pregenerate line numbers statically
            // to make the page load faster and avoid JavaScript cost
            bool pregenerateLineNumbers = IsLargeFile(lineCount);

            // pass a value larger than 0 to generate line numbers in JavaScript (to reduce HTML size)
            var prefix = Markup.GetDocumentPrefix(title, relativePathToRoot, pregenerateLineNumbers ? 0 : lineCount);
            writer.Write(prefix);
            GenerateHeader(writer.WriteLine);

            var ranges = (await classifier.Classify(Document, Text)).ToArray();

            // pass a value larger than 0 to generate line numbers statically at HTML generation time
            var table = Markup.GetTablePrefix(DocumentUrl, pregenerateLineNumbers ? lineCount : 0, GenerateGlyphs(ranges));
            writer.WriteLine(table);

            GeneratePre(ranges, writer, lineCount);
            var suffix = Markup.GetDocumentSuffix();
            writer.WriteLine(suffix);
        }

        private ISymbol GetSymbolForRange(Classification.Range r)
        {
            var position = r.ClassifiedSpan.TextSpan.Start;
            var token = Root.FindToken(position, findInsideTrivia: true);

            if (token != null)
            {
                return SemanticModel.GetDeclaredSymbol(token.Parent);
            }
            return null;
        }

        private string GenerateGlyphs(IEnumerable<Classification.Range> ranges)
        {
            var lines = new Dictionary<int, HashSet<string>>();
            int lineNumber = -1;
            ISymbol symbol = null;
            Dictionary<string, string> context = new Dictionary<string, string>
            {
                    { MEF.ContextKeys.FilePath, Document.FilePath },
                    { MEF.ContextKeys.LineNumber, "-1" }
            };

            Action<string> maybeLog = g =>
            {
                if (!string.IsNullOrWhiteSpace(g))
                {
                    HashSet<string> lineGlyphs;
                    if (!lines.TryGetValue(lineNumber, out lineGlyphs))
                    {
                        lineGlyphs = new HashSet<string>();
                        lines.Add(lineNumber, lineGlyphs);
                    }
                    lineGlyphs.Add(g);
                }
            };

            Func<MEF.ITextVisitor, string> VisitText = v =>
            {
                try
                {
                    return v.Visit(Text.Lines[lineNumber - 1].ToString(), context);
                }
                catch (Exception ex)
                {
                    Log.Write("Exception in text visitor: " + ex.Message);
                    return null;
                }
            };

            Func<MEF.ISymbolVisitor, string> VisitSymbol = v =>
            {
                try
                {
                    return symbol.Accept(new MEF.SymbolVisitorWrapper(v, context));
                }
                catch (Exception ex)
                {
                    Log.Write("Exception in symbol visitor: " + ex.Message);
                    return null;
                }
            };


            foreach (var r in ranges)
            {
                var pos = r.ClassifiedSpan.TextSpan.Start;
                var token = Root.FindToken(pos, true);
                var nextLineNumber = token.SyntaxTree.GetLineSpan(token.Span).StartLinePosition.Line + 1;

                if (nextLineNumber != lineNumber)
                {
                    lineNumber = nextLineNumber;
                    context[MEF.ContextKeys.LineNumber] = lineNumber.ToString();
                    maybeLog(string.Concat(projectGenerator.PluginTextVisitors.Select(VisitText)));
                }
                symbol = SemanticModel.GetDeclaredSymbol(token.Parent);
                if (symbol != null)
                {
                    maybeLog(string.Concat(projectGenerator.PluginSymbolVisitors.Select(VisitSymbol)));
                }
            }
            if (lines.Any())
            {
                var sb = new StringBuilder();
                for (var i = 1; i <= lines.Keys.Max(); i++)
                {
                    HashSet<string> glyphs;
                    if (lines.TryGetValue(i, out glyphs))
                    {
                        foreach (var g in glyphs)
                        {
                            sb.Append(g);
                        }
                    }
                    sb.Append("<br/>");
                }
                return sb.ToString();
            }
            else
            {
                return string.Empty;
            }
        }



        private class ET4MethodVisitor : SymbolVisitor<string>
        {
        }

        private string DocumentUrl { get { return Document.Project.AssemblyName + "/" + documentRelativeFilePathWithoutHtmlExtension.Replace('\\', '/'); } }

        private void GenerateHeader(Action<string> writeLine)
        {
            string documentDisplayName = documentRelativeFilePathWithoutHtmlExtension;
            string projectDisplayName = projectGenerator.ProjectSourcePath;
            string projectUrl = "/#" + Document.Project.AssemblyName;

            string documentLink = string.Format("File: <a id=\"filePath\" class=\"blueLink\" href=\"{0}\" target=\"_top\">{1}</a><br/>", DocumentUrl, documentDisplayName);
            string projectLink = string.Format("Project: <a id=\"projectPath\" class=\"blueLink\" href=\"{0}\" target=\"_top\">{1}</a> ({2})", projectUrl, projectDisplayName, projectGenerator.AssemblyName);

            // TODO: Refactor after removal of fileshare/web links which should be plugins

            string firstRow = string.Format("<tr><td>{0}</td><td></td></tr>", documentLink);
            string secondRow = string.Format("<tr><td>{0}</td><td></td></tr>", projectLink);

            Markup.WriteLinkPanel(writeLine, firstRow, secondRow);
        }

        private async Task GeneratePre(StreamWriter writer, int lineCount = 0)
        {
            var ranges = await classifier.Classify(Document, Text);
            GeneratePre(ranges, writer, lineCount);
        }

        private void GeneratePre(IEnumerable<Classification.Range> ranges, StreamWriter writer, int lineCount = 0)
        {
            if (ranges == null)
            {
                // if there was an error in Roslyn, don't fail the entire index, just return
                return;
            }

            foreach (var range in ranges)
            {
                string html = GenerateRange(writer, range, lineCount);
                writer.Write(html);
            }
        }

        private bool IsLargeFile(int lineCount)
        {
            return lineCount > 30000;
        }

        private string GenerateRange(StreamWriter writer, Classification.Range range, int lineCount = 0)
        {
            var html = range.Text;
            html = Markup.HtmlEscape(html);
            bool isLargeFile = IsLargeFile(lineCount);
            string classAttributeValue = GetClassAttribute(html, range);
            HtmlElementInfo hyperlinkInfo = GenerateLinks(range, isLargeFile);

            if (hyperlinkInfo == null)
            {
                if (classAttributeValue == null || isLargeFile)
                {
                    return html;
                }

                if (classAttributeValue == "k")
                {
                    return "<b>" + html + "</b>";
                }

            }

            var sb = new StringBuilder();

            var elementName = "span";
            if (hyperlinkInfo != null)
            {
                elementName = hyperlinkInfo.Name;
            }

            sb.Append("<" + elementName);
            bool overridingClassAttributeSpecified = false;
            if (hyperlinkInfo != null)
            {
                foreach (var attribute in hyperlinkInfo.Attributes)
                {
                    AddAttribute(sb, attribute.Key, attribute.Value);
                    if (attribute.Key == "class")
                    {
                        overridingClassAttributeSpecified = true;
                    }
                }
            }

            if (!overridingClassAttributeSpecified)
            {
                AddAttribute(sb, "class", classAttributeValue);
            }

            sb.Append('>');

            html = AddIdSpanForImplicitConstructorIfNecessary(hyperlinkInfo, html);

            sb.Append(html);
            sb.Append("</" + elementName + ">");

            html = sb.ToString();

            if (hyperlinkInfo != null && hyperlinkInfo.DeclaredSymbol != null)
            {
                writer.Flush();
                long streamPosition = writer.BaseStream.Length;

                streamPosition += html.IndexOf(hyperlinkInfo.Attributes["id"] + ".html");
                projectGenerator.AddDeclaredSymbol(
                    hyperlinkInfo.DeclaredSymbol,
                    hyperlinkInfo.DeclaredSymbolId,
                    documentRelativeFilePathWithoutHtmlExtension,
                    streamPosition);
            }

            return html;
        }

        private string AddIdSpanForImplicitConstructorIfNecessary(HtmlElementInfo hyperlinkInfo, string html)
        {
            if (hyperlinkInfo != null && hyperlinkInfo.DeclaredSymbol != null)
            {
                INamedTypeSymbol namedTypeSymbol = hyperlinkInfo.DeclaredSymbol as INamedTypeSymbol;
                if (namedTypeSymbol != null)
                {
                    var implicitInstanceConstructor = namedTypeSymbol.Constructors.FirstOrDefault(c => !c.IsStatic && c.IsImplicitlyDeclared);
                    if (implicitInstanceConstructor != null)
                    {
                        var symbolId = SymbolIdService.GetId(implicitInstanceConstructor);
                        html = Markup.Tag("span", html, new Dictionary<string, string> { { "id", symbolId } });
                        projectGenerator.AddDeclaredSymbol(
                            implicitInstanceConstructor,
                            symbolId,
                            documentRelativeFilePathWithoutHtmlExtension,
                            0);
                    }
                }
            }

            return html;
        }

        private void AddAttribute(StringBuilder sb, string name, string value)
        {
            if (value != null)
            {
                sb.Append(" " + name + "=\"" + value + "\"");
            }
        }
    }
}
