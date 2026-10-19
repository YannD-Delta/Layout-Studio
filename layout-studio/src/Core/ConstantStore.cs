using System;
using System.Collections.Generic;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>Typed constants with their defaults and current values. Pure logic, no WPF.</summary>
    public class ConstantStore
    {
        private readonly Dictionary<string, ConstantDef> defs = new Dictionary<string, ConstantDef>();
        private readonly Dictionary<string, string> values = new Dictionary<string, string>();

        /// <summary>Raised after a value really changed (not when the same value is set again).</summary>
        public event Action<string> ValueChanged;

        /// <summary>How many handlers listen to ValueChanged (a view model that is gone must not be among them).</summary>
        public int SubscriberCount { get { var h = ValueChanged; return h == null ? 0 : h.GetInvocationList().Length; } }

        /// <summary>Remembers the handlers now attached; the returned action detaches every handler added after this call.</summary>
        public Action MarkSubscribers()
        {
            var before = ValueChanged == null ? new Delegate[0] : ValueChanged.GetInvocationList();
            return () =>
            {
                var now = ValueChanged;
                if (now == null) return;
                foreach (var h in now.GetInvocationList())
                    if (Array.IndexOf(before, h) < 0) ValueChanged -= (Action<string>)h;
            };
        }

        public ConstantStore(IEnumerable<ConstantDef> definitions)
        {
            foreach (var def in definitions)
            {
                string normalized, error;
                // A default that does not normalize is kept verbatim so a broken schema never hides a constant.
                defs[def.Key] = def;
                values[def.Key] = ConstantValue.TryNormalize(def, def.Default, out normalized, out error) ? normalized : def.Default;
            }
        }

        public IEnumerable<ConstantDef> Definitions { get { return defs.Values; } }

        public ConstantDef Find(string key)
        {
            ConstantDef def;
            return defs.TryGetValue(key, out def) ? def : null;
        }

        public string Get(string key)
        {
            string value;
            return values.TryGetValue(key, out value) ? value : null;
        }

        public string GetDefault(string key)
        {
            ConstantDef def;
            if (!defs.TryGetValue(key, out def)) return null;
            string normalized, error;
            return ConstantValue.TryNormalize(def, def.Default, out normalized, out error) ? normalized : def.Default;
        }

        public bool TrySet(string key, string raw, out string error)
        {
            ConstantDef def;
            if (!defs.TryGetValue(key, out def))
            {
                error = "unknown constant '" + key + "'";
                return false;
            }
            string normalized;
            if (!ConstantValue.TryNormalize(def, raw, out normalized, out error)) return false;
            if (values[key] != normalized)
            {
                values[key] = normalized;
                var handler = ValueChanged;
                if (handler != null) handler(key);
            }
            return true;
        }

        public void Reset(string key)
        {
            ConstantDef def;
            if (!defs.TryGetValue(key, out def)) return;
            string error;
            TrySet(key, GetDefault(key), out error);
        }

        public ICollection<string> Changed()
        {
            return defs.Keys.Where(k => values[k] != GetDefault(k)).ToList();
        }
    }
}
