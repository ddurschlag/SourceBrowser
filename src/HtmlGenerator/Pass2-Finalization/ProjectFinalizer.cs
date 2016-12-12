using System;
using System.Collections.Generic;
using Path = System.IO.Path;
using System.Linq;
using Microsoft.SourceBrowser.Common;
using Microsoft.SourceBrowser.Common.Entity;

namespace Microsoft.SourceBrowser.HtmlGenerator
{
    public partial class ProjectFinalizer
    {
        private string projectSourcePath;
        public SolutionFinalizer SolutionFinalizer;

        public string AssemblyId { get; private set; }
        public string[] ReferencedAssemblies { get; set; }
        public List<string> ReferencingAssemblies { get; private set; }
        public Dictionary<ulong, DeclaredSymbolInfo> DeclaredSymbols { get; set; }
        public Dictionary<ulong, Tuple<string, ulong>> BaseMembers { get; set; }
        public MultiDictionary<ulong, Tuple<string, ulong>> ImplementedInterfaceMembers { get; set; }
        public IO.ProjectManager IOManager;

        public long DocumentCount { get; set; }
        public long LinesOfCode { get; set; }
        public long BytesOfCode { get; set; }
        public long DeclaredSymbolCount { get; set; }
        public long DeclaredTypeCount { get; set; }
        public long PublicTypeCount { get; set; }

        public ProjectFinalizer(SolutionFinalizer solutionFinalizer, IO.ProjectManager ioManager)
        {
            this.BaseMembers = new Dictionary<ulong, Tuple<string, ulong>>();
            this.ImplementedInterfaceMembers = new MultiDictionary<ulong, Tuple<string, ulong>>();
            this.SolutionFinalizer = solutionFinalizer;
            IOManager = ioManager;
            ReferencingAssemblies = new List<string>();
            this.AssemblyId = IOManager.AssemblyId;
            ReadProjectInfo();
            ReadDeclarationLines();
            ReadBaseMembers();
            ReadImplementedInterfaceMembers();
        }

        public override string ToString()
        {
            return AssemblyId;
        }

        public void ReadDeclarationLines()
        {
            DeclaredSymbols = new Dictionary<ulong, DeclaredSymbolInfo>();
            foreach (var declarationLine in IOManager.GetDeclaredSymbolLines())
            {
                var symbolInfo = new Utilities.DeclaredSymbolInfoFactory().Manufacture(declarationLine);
                symbolInfo.AssemblyName = this.AssemblyId;
                if (symbolInfo.IsValid)
                {
                    DeclaredSymbols[symbolInfo.ID] = symbolInfo;
                }
            }
        }

        public string ProjectInfoLine
        {
            get
            {
                return projectSourcePath;
            }
        }

        private void ReadBaseMembers()
        {
            foreach (var line in IOManager.GetBaseMemberLines())
            {
                var parts = line.Split(';');
                var derivedId = TextUtilities.HexStringToULong(parts[0]);
                var baseAssemblyName = string.Intern(parts[1]);
                var baseId = TextUtilities.HexStringToULong(parts[2]);
                BaseMembers[derivedId] = Tuple.Create(baseAssemblyName, baseId);
            }
        }

        private void ReadImplementedInterfaceMembers()
        {
            foreach (var line in IOManager.GetImplementedInterfaceMemberLines())
            {
                var parts = line.Split(';');
                var implementationId = TextUtilities.HexStringToULong(parts[0]);
                var interfaceAssemblyName = string.Intern(parts[1]);
                var interfaceMemberId = TextUtilities.HexStringToULong(parts[2]);
                ImplementedInterfaceMembers.Add(implementationId, Tuple.Create(interfaceAssemblyName, interfaceMemberId));
            }
        }

        private void ReadProjectInfo()
        {
            var lines = IOManager.GetProjectInfoLines();
            if (lines.Any())
            {
                projectSourcePath = Serialization.ReadValue(lines, "ProjectSourcePath");
                DocumentCount = Serialization.ReadLong(lines, "DocumentCount");
                LinesOfCode = Serialization.ReadLong(lines, "LinesOfCode");
                BytesOfCode = Serialization.ReadLong(lines, "BytesOfCode");
                DeclaredSymbolCount = Serialization.ReadLong(lines, "DeclaredSymbols");
                DeclaredTypeCount = Serialization.ReadLong(lines, "DeclaredTypes");
                PublicTypeCount = Serialization.ReadLong(lines, "PublicTypes");
            }

            lines = IOManager.GetReferencedAssemblyLines();
            if (lines.Any())
            {
                ReferencedAssemblies = lines.Select(string.Intern).ToArray();
            }
        }
    }
}
