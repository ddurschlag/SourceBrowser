using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.SourceBrowser.Common.Entity
{
    public class DeclaredSymbolInfo : IEquatable<DeclaredSymbolInfo>, IComparable<DeclaredSymbolInfo>
    {
        public ushort AssemblyNumber;
        public string AssemblyName { get; set; }
        public string ProjectFilePath { get; set; }
        public ushort Glyph;
        public string Name;
        public ulong ID;
        public string Kind;
        public ushort KindRank;
        public string Description;
        public ushort MatchLevel;

        public DeclaredSymbolInfo()
        {
        }

        public DeclaredSymbolInfo(ushort glyph, ushort assemblyNumber = 0)
        {
            Glyph = glyph;
            AssemblyNumber = assemblyNumber;
        }

        public DeclaredSymbolInfo(ulong id, string name, string kind, ushort kindRank, string description, ushort glyph, string assemblyName = null)
        {
            ID = id;
            Name = name;
            Kind = kind;
            KindRank = kindRank;
            Description = description;
            Glyph = glyph;
            AssemblyName = assemblyName;
        }

        public bool IsValid
        {
            get
            {
                return Glyph != ushort.MaxValue;
            }
        }

        public string GetNamespace()
        {
            var description = Description;
            if (string.IsNullOrEmpty(description))
            {
                return "";
            }

            int lastDot = description.LastIndexOf('.');
            if (lastDot == -1)
            {
                return "";
            }

            return description.Substring(0, lastDot);
        }

        public int Weight
        {
            get
            {
                return MatchLevel * 10 + KindRank;
            }
        }

        public bool Equals(DeclaredSymbolInfo other)
        {
            if (other == null)
            {
                return false;
            }

            return
                AssemblyNumber == other.AssemblyNumber &&
                ProjectFilePath == other.ProjectFilePath &&
                Glyph == other.Glyph &&
                Name == other.Name &&
                Kind == other.Kind &&
                ID == other.ID &&
                Description == other.Description;
        }

        public string GetUrl()
        {
            return "/" + AssemblyName + "/a.html#" + TextUtilities.ULongToHexString(ID);
        }

        public override bool Equals(object obj)
        {
            DeclaredSymbolInfo other = obj as DeclaredSymbolInfo;
            if (other == null)
            {
                return false;
            }

            return Equals(other);
        }

        public override int GetHashCode()
        {
            return
                AssemblyNumber.GetHashCode() ^
                ProjectFilePath.GetHashCode() ^
                Glyph.GetHashCode() ^
                Name.GetHashCode() ^
                Kind.GetHashCode() ^
                Description.GetHashCode() ^
                ID.GetHashCode();
        }

        public int CompareTo(DeclaredSymbolInfo other)
        {
            if (this == other)
            {
                return 0;
            }

            if (this == null || other == null)
            {
                return 1;
            }

            int comparison = StringComparer.OrdinalIgnoreCase.Compare(this.Name, other.Name);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = this.KindRank.CompareTo(other.KindRank);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.Ordinal.Compare(this.Name, other.Name);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = this.AssemblyNumber.CompareTo(other.AssemblyNumber);
            return comparison;
        }
    }
}