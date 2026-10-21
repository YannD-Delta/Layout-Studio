using System;
using System.Collections.Generic;
using System.Linq;

namespace LayoutStudio.Core
{
    /// <summary>One entry of the completion status picker: the Playnite status id (empty = no status), its name and whether it is the game's current one.</summary>
    public class CompletionChoice
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsCurrent { get; set; }
    }

    /// <summary>
    /// The list the header card's picker shows: "none" first, then Playnite's own completion statuses in their order. Pure logic: the plugin feeds it the
    /// statuses of the database and writes the chosen id back to the game.
    /// </summary>
    public static class CompletionChoices
    {
        public static IList<CompletionChoice> Build(IEnumerable<KeyValuePair<string, string>> statuses, string currentId, string noneName)
        {
            var list = new List<CompletionChoice> { new CompletionChoice { Id = "", Name = noneName } };
            foreach (var s in statuses ?? Enumerable.Empty<KeyValuePair<string, string>>())
                list.Add(new CompletionChoice { Id = s.Key, Name = (s.Value ?? "").Trim() });
            var current = list.FirstOrDefault(c => c.Id.Length > 0 && string.Equals(c.Id, currentId, StringComparison.OrdinalIgnoreCase)) ?? list[0];
            current.IsCurrent = true;
            return list;
        }

        /// <summary>The Playnite id for a picker value: an empty or invalid value means no status.</summary>
        public static Guid ToGuid(string id)
        {
            Guid g;
            return Guid.TryParse(id, out g) ? g : Guid.Empty;
        }
    }
}
