using LayoutStudio.Core;

namespace LayoutStudio.Editor
{
    /// <summary>Applies a preset choice as one undo step that also restores the profile's recorded selection.</summary>
    public static class PresetHistory
    {
        public static void Apply(History history, ProfileApplier applier, Profile profile, string group, string choiceId)
        {
            string before;
            if (!profile.Presets.TryGetValue(group, out before)) before = applier.DefaultChoiceOf(group);
            history.BeginBatch();
            try
            {
                applier.ApplyPreset(profile, group, choiceId);
                string previous = before;
                history.RecordAction(
                    () => { if (previous != null) applier.ApplyPreset(profile, group, previous); },
                    () => applier.ApplyPreset(profile, group, choiceId));
            }
            finally { history.EndBatch(); }
        }
    }
}
