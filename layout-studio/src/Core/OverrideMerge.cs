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
            return Rebuild(profile, store, null);
        }

        /// <summary>
        /// Same, but a constant that holds exactly the value the shared Look wants is not saved as this mode's own override: it stays the Look's,
        /// so a later change of the Look reaches this mode. A value moved away from the Look is the mode's own choice and is kept.
        /// </summary>
        public static int Rebuild(Profile profile, ConstantStore store, System.Collections.Generic.IDictionary<string, string> lookWanted)
        {
            var foreign = profile.Overrides.Where(kv => store.Find(kv.Key) == null).ToList();
            profile.Overrides.Clear();
            foreach (var kv in foreign) profile.Overrides[kv.Key] = kv.Value;
            foreach (var key in store.Changed())
            {
                string wanted;
                if (lookWanted != null && lookWanted.TryGetValue(key, out wanted) && wanted == store.Get(key)) continue;
                profile.Overrides[key] = store.Get(key);
            }
            return foreign.Count;
        }
    }
}
