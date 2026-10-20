using System.Collections.Generic;
using System.Text;

namespace LayoutStudio.Core
{
    public enum DiagnosticLevel { Info, Ok, Warning }

    /// <summary>One fact about the install: what, its value, and whether it is fine.</summary>
    public class DiagnosticLine
    {
        public string Title;
        public string Value;
        public DiagnosticLevel Level;
        public string Detail;   // longer explanation, optional

        public DiagnosticLine() { }
        public DiagnosticLine(string title, string value, DiagnosticLevel level = DiagnosticLevel.Info, string detail = null)
        {
            Title = title; Value = value; Level = level; Detail = detail;
        }
    }

    public static class Diagnostics
    {
        /// <summary>The lines as plain text to paste into a bug report. Nothing personal: names of settings and counts only.</summary>
        public static string Format(IEnumerable<DiagnosticLine> lines)
        {
            var sb = new StringBuilder();
            foreach (var l in lines)
            {
                string mark = l.Level == DiagnosticLevel.Warning ? "[!] " : l.Level == DiagnosticLevel.Ok ? "[ok] " : "     ";
                sb.Append(mark).Append(l.Title).Append(": ").Append(l.Value);
                if (!string.IsNullOrEmpty(l.Detail)) sb.Append("  (").Append(l.Detail).Append(')');
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
