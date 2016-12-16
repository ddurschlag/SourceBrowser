﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.SourceBrowser.Common.Entity;

namespace Microsoft.SourceBrowser.HtmlGenerator.Utilities
{
    public class DeclaredSymbolInfoFactory
    {
        public DeclaredSymbolInfo Manufacture(ISymbol symbol, string assemblyName = null)
        {
            string kind = SymbolKindText.GetSymbolKind(symbol);

            return new DeclaredSymbolInfo(
                SymbolIdService.GetIdULong(symbol),
                SymbolIdService.GetName(symbol),
                kind,
                SymbolKindText.Rank(kind),
                SymbolIdService.GetDisplayString(symbol),
                SymbolIdService.GetGlyphNumber(symbol),
                assemblyName
            );
        }

        public DeclaredSymbolInfo Manufacture(string separated, ushort assemblyNumber = 0)
        {
            ushort glyph = ushort.MaxValue; // to save space and avoid extra field, this indicates an invalid symbol

            var parts = separated.Split(';');
            if (parts.Length == 5)
            {
                return Manufacture(
                    parts[1],
                    string.Intern(parts[0]),
                    string.Intern(parts[2]),
                    parts[3],
                    parts[4]
                );
            }
            else
            {
                return new DeclaredSymbolInfo(glyph, assemblyNumber);
            }
        }

        public DeclaredSymbolInfo Manufacture(string hexStringId, string name, string kind, string description, string glyph)
        {
            return new DeclaredSymbolInfo(
                Common.TextUtilities.HexStringToULong(hexStringId),
                name,
                kind,
                SymbolKindText.Rank(kind),
                description,
                ParseGlyph(glyph)
            );
        }

        private static ushort ParseGlyph(string part)
        {
            ushort value;
            if (ushort.TryParse(part, out value))
                return value;
            else
                return ushort.MaxValue;
        }
    }
}
