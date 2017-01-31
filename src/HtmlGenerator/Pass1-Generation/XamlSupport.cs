namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public class XamlSupport : XmlSupport
    {
        private ProjectGenerator projectGenerator;
        private string relativePath;

        public XamlSupport(ProjectGenerator projectGenerator)
        {
            this.projectGenerator = projectGenerator;
        }

        internal void GenerateXaml(string sourceXmlFile, string destinationHtmlFile, string relativePath)
        {
            this.relativePath = relativePath;

            var ioManager = projectGenerator.IOManager;

            base.Generate(sourceXmlFile, ioManager.GetFileText(sourceXmlFile), ioManager.GetFileLineCount(sourceXmlFile), destinationHtmlFile, ioManager);
        }

        protected override string GetAssemblyName()
        {
            return projectGenerator.AssemblyName;
        }

        protected override string GetDisplayName()
        {
            return relativePath;
        }
    }
}
