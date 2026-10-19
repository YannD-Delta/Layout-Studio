using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>Rebuilds a profile's overrides from the store without losing the keys the current theme does not define.</summary>
    public static class OverrideMerge
    {
        /// <summary>
        /// A key the theme no longer has (renamed, removed, or saved by a newer theme) is harmless: it is never applied. It must survive a save, so
        /// a theme update, a rollback or a share code from a newer version never silently throws a choice away.
        /// </summary>
        public static int Rebuild(Profile profile, ConstantStore store)
        {
            var foreign = profile.Overrides.Where(kv => store.Find(kv.Key) == null).ToList();
            profile.Overrides.Clear();
            foreach (var kv in foreign) profile.Overrides[kv.Key] = kv.Value;
            foreach (var key in store.Changed()) profile.Overrides[key] = store.Get(key);
            return foreign.Count;
        }
    }
}
