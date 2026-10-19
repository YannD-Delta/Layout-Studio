using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace LayoutStudio.Core
{
    /// <summary>A width/height pair of constants shown as a resizable box in the sizes editor.</summary>
    public class SizeDef
    {
        public string Id;
        public string Title;
        public string WidthKey;
        public string HeightKey;   // the same key as WidthKey means a square
        public string Description;
        public string Group;
        public string Live;            // when the box is on screen: always | view:details | ...
        public string WhereLabel;
        public string View;            // any | details | grid | details+grid
        public double[] Rect = new double[4];   // x, y, w, h as fractions of the main window
        public string RatioDefault;    // "2:3", "current", "free"; null when the item has no ratio row
        public bool RatioLocked;
        public bool HasRatio { get { return RatioDefault != null; } }
    }

    /// <summary>A rounded element of the Corners page; Round is its radius in the Round look (100 % of the master slider).</summary>
    public class CornerDef
    {
        public string Key;
        public string Title;
        public double Round;
    }

    public class SizeGroup
    {
        public string Id;
        public string Title;
    }

    /// <summary>One window-background setting of the visual editor.</summary>
    public class TuningDef
    {
        public string Key;
        public string Title;
        public string Kind;        // toggle | slider
    }

    /// <summary>A brush resource whose Color or Opacity is driven by a constant.</summary>
    public class Dependent
    {
        public string Resource;
        public string Property;   // Color | Opacity
    }

    public class Choice
    {
        public string Id;
        public string Name;
        public string LocKey;
        public bool Structural;      // its files replace control styles/templates: merged only at startup, never under a running window
        public Dictionary<string, string> Swatch = new Dictionary<string, string>();   // bg1, bg2, text, accent (color profiles and accents)
        public string Preview;       // path of a picture of this choice, relative to the theme folder (Presets/Previews/...)
        public string Value;         // tileSlots: the Int32 stored in the slot constant for this tile type
        public Dictionary<string, string> Constants = new Dictionary<string, string>();
        public List<string> Files = new List<string>();
    }

    public class Control
    {
        public string Kind;          // toggle | slider | number | choice | color | thickness | orderedList | tileSlots | preset
        public string Id;
        public List<string> Keys = new List<string>();
        public List<string> ExtraKeys = new List<string>();    // tileSlots: three more keys per slot (B, C, D), in slot order
        public List<string> ItemTitles = new List<string>();   // orderedList: one title per key
        public string Title;
        public string TitleKey;
        public string Description;
        public string TipKey;
        public bool NeedsRestart;
        public string DefaultChoice;
        public List<Choice> Choices = new List<Choice>();
    }

    public class Group
    {
        public string Id;
        public string Title;
        public List<Control> Controls = new List<Control>();
    }

    public class Page
    {
        public string Id;
        public string Title;
        public List<Group> Groups = new List<Group>();
    }

    /// <summary>The settings model: pages of groups of controls, plus every constant's definition. Loaded from JSON.</summary>
    public class Schema
    {
        private readonly Dictionary<string, ConstantDef> constants = new Dictionary<string, ConstantDef>();

        public int Version;
        public List<Page> Pages = new List<Page>();
        public List<SectionDef> Sections = new List<SectionDef>();
        public List<SizeDef> Sizes = new List<SizeDef>();
        public List<SizeGroup> SizeGroups = new List<SizeGroup>();
        public List<CornerDef> Corners = new List<CornerDef>();
        public List<TuningDef> Tuning = new List<TuningDef>();
        private readonly HashSet<string> restartKeys = new HashSet<string>();

        /// <summary>True when the theme reads this constant with {StaticResource}: a change only shows after a restart.</summary>
        public bool NeedsRestart(string key) { return restartKeys.Contains(key); }
        private readonly Dictionary<string, List<Dependent>> dependents = new Dictionary<string, List<Dependent>>();

        public IList<Dependent> DependentsOf(string key)
        {
            List<Dependent> list;
            return dependents.TryGetValue(key, out list) ? list : new List<Dependent>();
        }

        public IEnumerable<ConstantDef> AllConstants() { return constants.Values; }

        /// <summary>Consistency of the visual editors' tables with the constants: every key they name exists, tile slots have their three extra keys, a movable section has both order keys.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            Action<string, string> need = (where, key) =>
            {
                if (!string.IsNullOrEmpty(key) && !constants.ContainsKey(key)) problems.Add(where + " uses " + key + ", which is not a constant");
            };
            foreach (var c in Corners) need("corner " + c.Key, c.Key);
            foreach (var t in Tuning) need("tuning " + t.Key, t.Key);
            foreach (var z in Sizes) { need("size " + z.Id, z.WidthKey); need("size " + z.Id, z.HeightKey); }
            foreach (var sec in Sections)
            {
                need("section " + sec.Id, sec.LeftKey); need("section " + sec.Id, sec.RightKey); need("section " + sec.Id, sec.ShowKey);
                need("section " + sec.Id, sec.ExpandKey); need("section " + sec.Id, sec.ColumnKey);
                if (sec.ColumnKey != null && (sec.LeftKey == null || sec.RightKey == null))
                    problems.Add("section " + sec.Id + " can change column but lacks a left or right order key");
            }
            foreach (var control in Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls))
            {
                foreach (var key in control.Keys) need("control " + control.Id, key);
                if (control.Kind == "tileSlots" && control.ExtraKeys.Count != 3 * control.Keys.Count)
                    problems.Add("control " + control.Id + " has " + control.ExtraKeys.Count + " extra keys for " + control.Keys.Count + " slots (3 each expected)");
            }
            return problems;
        }

        public ConstantDef Find(string key)
        {
            ConstantDef def;
            return constants.TryGetValue(key, out def) ? def : null;
        }

        public static Schema Load(string json)
        {
            object root;
            try
            {
                root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 100 }.DeserializeObject(json);
            }
            catch (Exception ex)
            {
                throw new FormatException("schema is not valid JSON: " + ex.Message, ex);
            }
            var obj = root as IDictionary<string, object>;
            if (obj == null) throw new FormatException("schema root must be an object");

            var schema = new Schema { Version = ToInt(Get(obj, "version")) };
            foreach (var c in List(Get(obj, "constants")))
            {
                var d = (IDictionary<string, object>)c;
                var def = new ConstantDef
                {
                    Key = Str(d, "key"),
                    Type = (ConstantType)Enum.Parse(typeof(ConstantType), Str(d, "type")),
                    Default = Str(d, "default"),
                    Min = Num(d, "min"),
                    Max = Num(d, "max"),
                    Step = Num(d, "step")
                };
                schema.constants[def.Key] = def;
            }
            foreach (var item in List(Get(obj, "sections")))
            {
                var d = (IDictionary<string, object>)item;
                schema.Sections.Add(new SectionDef
                {
                    Id = Str(d, "id"), Title = Str(d, "title"), LeftKey = Str(d, "leftKey"), RightKey = Str(d, "rightKey"),
                    ShowKey = Str(d, "showKey"), ExpandKey = Str(d, "expandKey"), ColumnKey = Str(d, "columnKey")
                });
            }
            foreach (var item in List(Get(obj, "sizes")))
            {
                var d = (IDictionary<string, object>)item;
                var size = new SizeDef { Id = Str(d, "id"), Title = Str(d, "title"), WidthKey = Str(d, "widthKey"), HeightKey = Str(d, "heightKey"),
                    Description = Str(d, "description"), Group = Str(d, "group"), Live = Str(d, "live") };
                var where = Get(d, "where") as IDictionary<string, object>;
                if (where != null)
                {
                    size.WhereLabel = Str(where, "label");
                    size.View = Str(where, "view");
                    int n = 0;
                    foreach (var v in List(Get(where, "rect"))) if (n < 4) size.Rect[n++] = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                }
                var ratio = Get(d, "ratio") as IDictionary<string, object>;
                if (ratio != null)
                {
                    size.RatioDefault = Str(ratio, "default");
                    object locked = Get(ratio, "locked");
                    size.RatioLocked = locked is bool && (bool)locked;
                }
                schema.Sizes.Add(size);
            }
            foreach (var item in List(Get(obj, "corners")))
            {
                var d = (IDictionary<string, object>)item;
                schema.Corners.Add(new CornerDef { Key = Str(d, "key"), Title = Str(d, "title"), Round = Num(d, "round") ?? 0 });
            }
            foreach (var item in List(Get(obj, "sizeGroups")))
            {
                var d = (IDictionary<string, object>)item;
                schema.SizeGroups.Add(new SizeGroup { Id = Str(d, "id"), Title = Str(d, "title") });
            }
            foreach (var item in List(Get(obj, "tuning")))
            {
                var d = (IDictionary<string, object>)item;
                schema.Tuning.Add(new TuningDef { Key = Str(d, "key"), Title = Str(d, "title"), Kind = Str(d, "kind") });
            }
            foreach (var k in List(Get(obj, "restartKeys"))) schema.restartKeys.Add((string)k);
            var deps = Get(obj, "dependents") as IDictionary<string, object>;
            if (deps != null)
            {
                foreach (var kv in deps)
                {
                    var list = new List<Dependent>();
                    foreach (var item in List(kv.Value))
                    {
                        var d = (IDictionary<string, object>)item;
                        list.Add(new Dependent { Resource = Str(d, "resource"), Property = Str(d, "property") });
                    }
                    schema.dependents[kv.Key] = list;
                }
            }
            foreach (var p in List(Get(obj, "pages")))
            {
                var pd = (IDictionary<string, object>)p;
                var page = new Page { Id = Str(pd, "id"), Title = Str(pd, "title") };
                foreach (var g in List(Get(pd, "groups")))
                {
                    var gd = (IDictionary<string, object>)g;
                    var group = new Group { Id = Str(gd, "id"), Title = Str(gd, "title") };
                    foreach (var c in List(Get(gd, "controls"))) group.Controls.Add(ReadControl((IDictionary<string, object>)c));
                    page.Groups.Add(group);
                }
                schema.Pages.Add(page);
            }
            return schema;
        }

        private static Control ReadControl(IDictionary<string, object> d)
        {
            var c = new Control
            {
                Kind = Str(d, "kind"), Id = Str(d, "id"), Title = Str(d, "title"), TitleKey = Str(d, "titleKey"),
                Description = Str(d, "description"), TipKey = Str(d, "tipKey"), DefaultChoice = Str(d, "defaultChoice"),
                NeedsRestart = Get(d, "needsRestart") is bool && (bool)Get(d, "needsRestart")
            };
            foreach (var k in List(Get(d, "keys"))) c.Keys.Add((string)k);
            foreach (var k in List(Get(d, "extraKeys"))) c.ExtraKeys.Add((string)k);
            foreach (var t in List(Get(d, "itemTitles"))) c.ItemTitles.Add((string)t);
            foreach (var ch in List(Get(d, "choices")))
            {
                var cd = (IDictionary<string, object>)ch;
                var choice = new Choice { Id = Str(cd, "id"), Name = Str(cd, "name"), LocKey = Str(cd, "locKey"), Value = Str(cd, "value"), Preview = Str(cd, "preview"),
                    Structural = Get(cd, "structural") is bool && (bool)Get(cd, "structural") };
                var consts = Get(cd, "constants") as IDictionary<string, object>;
                if (consts != null) foreach (var kv in consts) choice.Constants[kv.Key] = Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
                foreach (var f in List(Get(cd, "files"))) choice.Files.Add((string)f);
                var sw = Get(cd, "swatch") as IDictionary<string, object>;
                if (sw != null) foreach (var kv in sw) choice.Swatch[kv.Key] = Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
                c.Choices.Add(choice);
            }
            return c;
        }

        private static object Get(IDictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) ? v : null;
        }

        private static string Str(IDictionary<string, object> d, string key)
        {
            var v = Get(d, key);
            return v == null ? null : Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        private static double? Num(IDictionary<string, object> d, string key)
        {
            var v = Get(d, key);
            return v == null ? (double?)null : Convert.ToDouble(v, CultureInfo.InvariantCulture);
        }

        private static int ToInt(object v)
        {
            return v == null ? 0 : Convert.ToInt32(v, CultureInfo.InvariantCulture);
        }

        private static IEnumerable<object> List(object v)
        {
            var arr = v as IEnumerable;
            return arr == null ? Enumerable.Empty<object>() : arr.Cast<object>();
        }
    }
}
