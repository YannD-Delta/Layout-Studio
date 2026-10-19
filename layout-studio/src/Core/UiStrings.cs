using System;
using System.Globalization;

namespace LayoutStudio.Core
{
    /// <summary>
    /// The words of the editor's own texts (advice page, banner) in the user's language. The English text sits in the code and is the fallback:
    /// a missing or broken translation never breaks the page. <see cref="Lookup"/> is set once the editor window has loaded its dictionary.
    /// </summary>
    public class UiStrings
    {
        private Func<string, string> lookup;

        /// <summary>Raised when the dictionary is set, so texts built before it are built again.</summary>
        public event Action Changed;

        public Func<string, string> Lookup
        {
            get { return lookup; }
            set { lookup = value; var handler = Changed; if (handler != null) handler(); }
        }

        public string Get(string key, string fallback)
        {
            string text = null;
            try { if (Lookup != null) text = Lookup(key); }
            catch (Exception) { }
            return string.IsNullOrEmpty(text) ? fallback : text;
        }

        public string Format(string key, string fallback, params object[] args)
        {
            string format = Get(key, fallback);
            try { return string.Format(CultureInfo.InvariantCulture, format, args); }
            catch (FormatException) { return format; }
        }
    }
}
