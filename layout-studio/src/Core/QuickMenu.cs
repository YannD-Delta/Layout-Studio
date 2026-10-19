using System;
using System.Collections.Generic;
using System.Linq;

namespace LayoutStudio.Core
{
    public class QuickChoice
    {
        public string Id;
        public string Name;
    }

    /// <summary>One list the in-game quick menu offers: a preset group with its choices and the one in use.</summary>
    public class QuickMenuEntry
    {
        public string Id;
        public string Title;
        public string CurrentId;
        public List<QuickChoice> Choices = new List<QuickChoice>();
    }

    /// <summary>The Fullscreen quick menu: every preset group of the schema as a short list, picked with a controller. Pure logic, no UI.</summary>
    public static class QuickMenu
    {
        /// <summary>Preset groups in schema order. localize maps a localization key to text (null or an empty answer keeps the English name).</summary>
        public static List<QuickMenuEntry> Entries(Schema schema, Profile profile, Func<string, string> localize)
        {
            var entries = new List<QuickMenuEntry>();
            foreach (var control in schema.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Where(c => c.Kind == "preset"))
            {
                string current;
                if (!profile.Presets.TryGetValue(control.Id, out current)) current = control.DefaultChoice ?? "Default";
                var entry = new QuickMenuEntry { Id = control.Id, Title = Name(localize, control.TitleKey, control.Title), CurrentId = current };
                foreach (var choice in control.Choices) entry.Choices.Add(new QuickChoice { Id = choice.Id, Name = Name(localize, choice.LocKey, choice.Name) });
                entries.Add(entry);
            }
            return entries;
        }

        private static string Name(Func<string, string> localize, string key, string english)
        {
            var text = localize == null || string.IsNullOrEmpty(key) ? null : localize(key);
            return string.IsNullOrEmpty(text) ? english : text;
        }

        /// <summary>Applies one choice (constants and files) and records it in the profile; false when the group or choice is unknown.</summary>
        public static bool Choose(ProfileApplier applier, Profile profile, string groupId, string choiceId)
        {
            if (!applier.HasChoice(groupId, choiceId)) return false;
            applier.ApplyPreset(profile, groupId, choiceId);
            return true;
        }
    }
}
