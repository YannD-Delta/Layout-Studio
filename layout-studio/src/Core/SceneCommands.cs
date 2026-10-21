using System;
using System.Collections.Generic;
using System.Globalization;

namespace LayoutStudio.Core
{
    /// <summary>One line of a scene request: "key=value".</summary>
    public class SceneCommand
    {
        public string Key;
        public string Value;
        public override string ToString() { return Key + "=" + Value; }
    }

    /// <summary>
    /// Pure parsing for the testbed scene driver (tools\scene-shot.ps1 writes <c>scene.request</c>, Engine\SceneService.cs executes it).
    /// One command per line, "key=value"; blank lines and lines starting with # are ignored; keys are case-insensitive and lower-cased.
    /// </summary>
    public static class SceneCommands
    {
        public static List<SceneCommand> Parse(string text)
        {
            var list = new List<SceneCommand>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (var raw in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) throw new FormatException("scene line without key=value: " + line);
                list.Add(new SceneCommand { Key = line.Substring(0, eq).Trim().ToLowerInvariant(), Value = line.Substring(eq + 1).Trim() });
            }
            return list;
        }

        /// <summary>"1920x1080" gives true; "maximized" and "normal" give false (the caller handles them).</summary>
        public static bool TryParseSize(string text, out int width, out int height)
        {
            width = height = 0;
            if (string.IsNullOrEmpty(text)) return false;
            var parts = text.ToLowerInvariant().Split('x');
            return parts.Length == 2 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out width)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out height) && width > 0 && height > 0;
        }

        /// <summary>on / off / true / false / yes / no / 1 / 0.</summary>
        public static bool ParseSwitch(string text)
        {
            switch ((text ?? "").Trim().ToLowerInvariant())
            {
                case "on": case "true": case "yes": case "1": return true;
                case "off": case "false": case "no": case "0": return false;
            }
            throw new FormatException("not a switch (on/off): " + text);
        }

        /// <summary>"Genre:Action,Adventure" gives field Genre and two names. "clear" gives an empty field.</summary>
        public static void ParseFilter(string text, out string field, out string[] names)
        {
            field = null; names = new string[0];
            text = (text ?? "").Trim();
            if (text.Length == 0 || text.Equals("clear", StringComparison.OrdinalIgnoreCase)) { field = "clear"; return; }
            int colon = text.IndexOf(':');
            if (colon <= 0) { field = text; return; }   // a bare flag such as Favorite or IsInstalled
            field = text.Substring(0, colon).Trim();
            var items = new List<string>();
            foreach (var n in text.Substring(colon + 1).Split(','))
                if (n.Trim().Length > 0) items.Add(n.Trim());
            names = items.ToArray();
        }

        /// <summary>The scroll position of the details page: "top", "bottom" or a fraction 0..1 of the scrollable height.</summary>
        public static double ParseScroll(string text)
        {
            text = (text ?? "").Trim().ToLowerInvariant();
            if (text == "top") return 0;
            if (text == "bottom" || text == "end") return 1;
            double v;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 0 && v <= 1) return v;
            throw new FormatException("scroll wants top, bottom or a fraction 0..1: " + text);
        }
    }
}
