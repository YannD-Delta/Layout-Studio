using System;
using System.Collections.Generic;

namespace LayoutStudio.Core
{
    /// <summary>A named set of constant overrides and selected presets.</summary>
    public class Profile
    {
        public const int CurrentVersion = 2;

        public string Name;
        public int Version = CurrentVersion;
        public Dictionary<string, string> Overrides = new Dictionary<string, string>();
        public Dictionary<string, string> Presets = new Dictionary<string, string>();

        /// <summary>Editor choices that are not theme constants (for example the aspect ratio picked for the cover): key to text.</summary>
        public Dictionary<string, string> Editor = new Dictionary<string, string>();

        /// <summary>True when the file comes from a newer Layout Studio: it is read but never written back.</summary>
        public bool ReadOnly;

        /// <summary>Why the profile fell back to defaults or is read-only; null when everything is fine.</summary>
        public string Warning;
    }
}
