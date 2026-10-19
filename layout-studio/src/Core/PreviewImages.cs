using System.IO;

namespace LayoutStudio.Core
{
    /// <summary>Where the theme's preview pictures are: a choice stores its picture relative to the theme folder.</summary>
    public static class PreviewImages
    {
        public static string Root;

        /// <summary>Full path of a preview picture, or null when there is none or the file is missing.</summary>
        public static string PathOf(string relative)
        {
            if (string.IsNullOrEmpty(relative) || string.IsNullOrEmpty(Root)) return null;
            string full = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(full) ? full : null;
        }
    }
}
