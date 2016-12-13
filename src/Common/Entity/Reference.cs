using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.SourceBrowser.Common.Entity
{
    public enum ReferenceKind
    {
        DerivedType,
        InterfaceInheritance,
        InterfaceImplementation,
        Override,
        InterfaceMemberImplementation,
        Instantiation,
        Write,
        Read,
        Reference,
        GuidUsage,
        EmptyArrayAllocation,
        MSBuildPropertyAssignment,
        MSBuildPropertyUsage,
        MSBuildItemAssignment,
        MSBuildItemUsage,
        MSBuildTargetDeclaration,
        MSBuildTargetUsage,
        MSBuildTaskDeclaration,
        MSBuildTaskUsage
    }

    public class Reference
    {
        public string ToAssemblyId { get; set; }
        public string FromAssemblyId { get; set; }
        public string ToSymbolId { get; set; }
        public string FromLocalPath { get; set; }
        public string Url { get; set; }
        public string ReferenceLineText { get; set; }
        public int ReferenceColumnStart { get; set; }
        public int ReferenceColumnEnd { get; set; }
        public int ReferenceLineNumber { get; set; }
        public string ToSymbolName { get; set; }
        public ReferenceKind Kind { get; set; }

        public Reference()
        {
        }

        public Reference(
            string fromAssemblyId,
            string toAssemblyId,
            string url,
            string fromLocalPath,
            int referenceLineNumber,
            int referenceColumnStart,
            int referenceColumnEnd,
            string referenceLineText,
            ReferenceKind kind = default(ReferenceKind))
        {
            FromAssemblyId = fromAssemblyId;
            ToAssemblyId = toAssemblyId;
            Url = url;
            FromLocalPath = fromLocalPath;
            ReferenceLineNumber = referenceLineNumber;
            ReferenceColumnStart = referenceColumnStart;
            ReferenceColumnEnd = referenceColumnEnd;
            ReferenceLineText = referenceLineText;
            Kind = kind;
            ToSymbolName = ReferenceLineText.Substring(ReferenceColumnStart, ReferenceColumnEnd - ReferenceColumnStart);
        }
    }
}
