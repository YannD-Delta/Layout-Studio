using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LayoutStudio.Core;
using Playnite.SDK;

namespace LayoutStudio.Engine
{
    /// <summary>
    /// Test aid for tools\scene-shot.ps1, off unless LAYOUTSTUDIO_SHOT_DIR names a folder (same switch as ShotService). When "scene.request"
    /// appears there it is parsed (Core\SceneCommands.cs), executed on the UI thread, and "scene.request.done" is written with a few lines
    /// describing the result (window size, view, selected game...), or "scene.request.err" with the reason. Everything is reflection on
    /// Playnite's live settings object, the same way PlayniteSettingsAccess works: if Playnite's internals change, a command fails
    /// with a readable message and nothing else breaks.
    /// Commands: window=1920x1080|maximized|normal, view=details|grid|list, explorer=on|off, filterpanel=on|off, gridsidebar=on|off,
    /// explorerfield=Genre..., group=..., sort=..., filter=Genre:Action,Adventure | Favorite | clear, search=text, select=Game name,
    /// scroll=top|bottom|0..1, set=Path.To.Property:Value (any Playnite setting), locate=ElementName|type:ClassName (reports its place), probe=names.
    /// </summary>
    public static class SceneService
    {
        public static void Start(Dispatcher ui, IPlayniteAPI api, string dir, Action<string> log)
        {
            string request = Path.Combine(dir, "scene.request");
            var timer = new DispatcherTimer(DispatcherPriority.Background, ui) { Interval = TimeSpan.FromMilliseconds(300) };
            timer.Tick += (s, e) =>
            {
                if (!File.Exists(request)) return;
                string done = request + ".done", err = request + ".err";
                try
                {
                    string text = File.ReadAllText(request);
                    File.Delete(request);
                    if (File.Exists(done)) File.Delete(done);
                    if (File.Exists(err)) File.Delete(err);
                    var report = Run(api, SceneCommands.Parse(text));
                    File.WriteAllText(done, report);
                }
                catch (Exception ex)
                {
                    string where = "";
                    for (Exception inner = ex; inner != null; inner = inner.InnerException)
                    {
                        var xaml = inner as System.Windows.Markup.XamlParseException;
                        if (xaml != null) where += " [XAML " + xaml.BaseUri + " line " + xaml.LineNumber + "]";
                    }
                    string detail = ex.Message + where + (ex.InnerException != null ? " <- " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message : "");
                    if (log != null) log("scene failed: " + detail);
                    try { File.WriteAllText(err, detail); } catch (Exception) { }
                }
            };
            timer.Start();
        }

        private static string Run(IPlayniteAPI api, List<SceneCommand> commands)
        {
            var settings = PlayniteSettingsAccess.Raw(api);
            if (settings == null) throw new InvalidOperationException("Playnite settings object not reachable");
            var extra = new StringBuilder();
            foreach (var c in commands) Apply(api, settings, c, extra);
            return Describe(api, settings) + extra;
        }

        private static void Apply(IPlayniteAPI api, object settings, SceneCommand c, StringBuilder extra)
        {
            switch (c.Key)
            {
                case "window": SetWindow(c.Value); break;
                case "view": SwitchView(settings, c.Value); break;
                case "explorer": SetPanel(settings, "ExplorerPanelVisible", "ToggleExplorerPanelCommand", null, SceneCommands.ParseSwitch(c.Value)); break;
                case "filterpanel": SetPanel(settings, "FilterPanelVisible", null, new[] { "OpenFilterPanelCommand", "CloseFilterPanelCommand" }, SceneCommands.ParseSwitch(c.Value)); break;
                case "gridsidebar": SetPath(settings, "GridViewSideBarVisible", c.Value); break;
                case "explorerfield": SetPath(settings, "ViewSettings.SelectedExplorerField", c.Value); break;
                case "group": SetPath(settings, "ViewSettings.GroupingOrder", c.Value); break;
                case "sort": SetPath(settings, "ViewSettings.SortingOrder", c.Value); break;
                case "set":
                    int colon = c.Value.IndexOf(':');
                    if (colon <= 0) throw new FormatException("set wants Path:Value");
                    SetPath(settings, c.Value.Substring(0, colon), c.Value.Substring(colon + 1));
                    break;
                case "filter": SetFilter(api, settings, c.Value); break;
                case "search": SetPath(settings, "FilterSettings.Name", c.Value); break;
                case "select":
                    var game = api.Database.Games.FirstOrDefault(g => g.Name == c.Value);
                    if (game == null) throw new InvalidOperationException("no game named " + c.Value);
                    api.MainView.SelectGame(game.Id);
                    break;
                case "scroll": ScrollDetails(SceneCommands.ParseScroll(c.Value)); break;
                case "locate": Locate(c.Value, extra); break;
                case "probe": Probe(extra); break;
                default: throw new InvalidOperationException("unknown scene command " + c.Key);
            }
        }

        /// <summary>
        /// Playnite's own view model (the main window's data context) has commands that switch the view and open the panels; they do more than
        /// setting the bound setting (the top panel highlight, the active view), so they are used when found and the setting is the fallback.
        /// </summary>
        private static object ViewModel()
        {
            var main = Application.Current.MainWindow;
            var candidates = new List<object> { main == null ? null : main.DataContext };
            if (main != null) candidates.AddRange(FindAll<FrameworkElement>(main).Select(e => e.DataContext).Where(d => d != null).Distinct().Take(400));
            return candidates.FirstOrDefault(d => d != null && d.GetType().GetProperty("SwitchGridViewCommand") != null);
        }

        private static bool Run(object vm, string command)
        {
            if (vm == null || command == null) return false;
            var p = vm.GetType().GetProperty(command);
            var cmd = p == null ? null : p.GetValue(vm, null) as System.Windows.Input.ICommand;
            if (cmd == null) return false;
            cmd.Execute(null);
            return true;
        }

        private static void SwitchView(object settings, string value)
        {
            string name;
            switch (value.Trim().ToLowerInvariant())
            {
                case "details": name = "SwitchDetailsViewCommand"; break;
                case "grid": name = "SwitchGridViewCommand"; break;
                case "list": name = "SwitchListViewCommand"; break;
                default: throw new FormatException("view wants details, grid or list: " + value);
            }
            if (!Run(ViewModel(), name)) SetPath(settings, "ViewSettings.GamesViewType", value);
        }

        private static void SetPanel(object settings, string settingName, string toggleCommand, string[] openClose, bool on)
        {
            var prop = settings.GetType().GetProperty(settingName);
            bool current = (bool)prop.GetValue(settings, null);
            var vm = ViewModel();
            if (toggleCommand != null) { if (current != on && !Run(vm, toggleCommand)) prop.SetValue(settings, on, null); return; }
            if (!Run(vm, on ? openClose[0] : openClose[1]) && current != on) prop.SetValue(settings, on, null);
        }

        private static void SetWindow(string value)
        {
            var main = Application.Current.MainWindow;
            if (main == null) throw new InvalidOperationException("no main window");
            int w, h;
            if (SceneCommands.TryParseSize(value, out w, out h))
            {
                // an exact size in device independent pixels at the top-left corner: the capture is then w x h at any screen scale
                main.WindowState = WindowState.Normal;
                main.Left = 0; main.Top = 0; main.Width = w; main.Height = h;
            }
            else if (value.Equals("maximized", StringComparison.OrdinalIgnoreCase)) main.WindowState = WindowState.Maximized;
            else if (value.Equals("normal", StringComparison.OrdinalIgnoreCase)) main.WindowState = WindowState.Normal;
            else throw new FormatException("window wants WxH, maximized or normal: " + value);
        }

        /// <summary>Reports where an element sits in the main window (device independent pixels: x,y of its centre, then width and height): "locate=PART_ElemMainMenu" by name or "locate=type:ListViewItem" by class name.</summary>
        private static void Locate(string what, StringBuilder extra)
        {
            var main = Application.Current.MainWindow;
            bool byType = what.StartsWith("type:", StringComparison.OrdinalIgnoreCase);
            string key = byType ? what.Substring(5) : what;
            var el = FindAll<FrameworkElement>(main).FirstOrDefault(e => e.IsVisible && e.ActualWidth > 0 && (byType ? e.GetType().Name == key : e.Name == key));
            if (el == null) throw new InvalidOperationException("no visible element " + what);
            var centre = el.TransformToAncestor(main).Transform(new Point(el.ActualWidth / 2, el.ActualHeight / 2));
            extra.AppendLine("locate." + what + "=" + (int)centre.X + "," + (int)centre.Y + "," + (int)el.ActualWidth + "," + (int)el.ActualHeight);
        }

        /// <summary>Lists the names of the visible named elements and the class names of the item containers (to find something to locate).</summary>
        private static void Probe(StringBuilder extra)
        {
            var main = Application.Current.MainWindow;
            var all = FindAll<FrameworkElement>(main).Where(e => e.IsVisible).ToList();
            extra.AppendLine("names=" + string.Join(",", all.Where(e => !string.IsNullOrEmpty(e.Name)).Select(e => e.Name).Distinct().Take(300)));
            extra.AppendLine("types=" + string.Join(",", all.Select(e => e.GetType().Name).Where(n => n.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Row", StringComparison.OrdinalIgnoreCase) >= 0).Distinct().Take(60)));
        }

        private static void ScrollDetails(double fraction)
        {
            var main = Application.Current.MainWindow;
            var host = FindAll<ScrollViewer>(main).FirstOrDefault(v => v.Name == "PART_ScrollViewHost" && v.IsVisible && v.ScrollableHeight > 0);
            if (host == null) host = FindAll<ScrollViewer>(main).Where(v => v.IsVisible && v.ScrollableHeight > 0).OrderByDescending(v => v.ActualWidth * v.ActualHeight).FirstOrDefault();
            if (host == null) return;   // a page that fits: nothing to scroll
            host.ScrollToVerticalOffset(host.ScrollableHeight * fraction);
        }

        private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) yield break;
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var typed = child as T;
                if (typed != null) yield return typed;
                foreach (var deeper in FindAll<T>(child)) yield return deeper;
            }
        }

        /// <summary>Sets a (nested) property by name, converting the text to the property's type (enum, switch, number, text).</summary>
        private static void SetPath(object root, string path, string text)
        {
            object target = root;
            var parts = path.Split('.');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var p = target.GetType().GetProperty(parts[i]);
                if (p == null) throw new InvalidOperationException("no property " + parts[i] + " on " + target.GetType().Name);
                target = p.GetValue(target, null);
                if (target == null) throw new InvalidOperationException(parts[i] + " is empty");
            }
            var prop = target.GetType().GetProperty(parts[parts.Length - 1]);
            if (prop == null || !prop.CanWrite) throw new InvalidOperationException("cannot set " + path);
            prop.SetValue(target, Convert(prop.PropertyType, text), null);
        }

        private static object Convert(Type type, string text)
        {
            if (type == typeof(bool)) return SceneCommands.ParseSwitch(text);
            if (type.IsEnum) return Enum.Parse(type, text, true);
            if (type == typeof(string)) return text;
            if (type == typeof(Guid)) return Guid.Parse(text);
            return System.Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
        }

        private static void SetFilter(IPlayniteAPI api, object settings, string value)
        {
            var filters = settings.GetType().GetProperty("FilterSettings").GetValue(settings, null);
            string field; string[] names;
            SceneCommands.ParseFilter(value, out field, out names);
            var type = filters.GetType();
            if (field == "clear")
            {
                if (Run(ViewModel(), "ClearFiltersCommand")) return;
                // reset every set filter: item lists to a fresh empty instance, flags and the name to off
                foreach (var p in type.GetProperties())
                {
                    if (!p.CanWrite) continue;
                    if (p.PropertyType == typeof(bool) && p.Name != "UseAndFilteringStyle" && p.Name != "SuppressFilterChanges") p.SetValue(filters, false, null);
                    else if (p.PropertyType == typeof(string)) p.SetValue(filters, null, null);
                    else if (p.PropertyType.Name == "IdItemFilterItemProperties" || p.PropertyType.Name == "StringFilterItemProperties" || p.PropertyType.Name == "EnumFilterItemProperties")
                        p.SetValue(filters, null, null);
                }
                return;
            }
            var prop = type.GetProperty(field);
            if (prop == null) throw new InvalidOperationException("unknown filter " + field);
            if (names.Length == 0)
            {
                if (prop.PropertyType == typeof(bool)) { prop.SetValue(filters, true, null); return; }
                throw new FormatException("filter " + field + " wants Field:Name1,Name2");
            }
            var ids = IdsFor(api, field, names);
            // IdItemFilterItemProperties has a constructor taking List<Guid>
            prop.SetValue(filters, Activator.CreateInstance(prop.PropertyType, ids), null);
        }

        private static List<Guid> IdsFor(IPlayniteAPI api, string field, string[] names)
        {
            var db = api.Database;
            IEnumerable<KeyValuePair<Guid, string>> items;
            switch (field.ToLowerInvariant())
            {
                case "genre": items = db.Genres.Select(x => new KeyValuePair<Guid, string>(x.Id, x.Name)); break;
                case "platform": items = db.Platforms.Select(x => new KeyValuePair<Guid, string>(x.Id, x.Name)); break;
                case "developer": case "publisher": items = db.Companies.Select(x => new KeyValuePair<Guid, string>(x.Id, x.Name)); break;
                case "category": items = db.Categories.Select(x => new KeyValuePair<Guid, string>(x.Id, x.Name)); break;
                case "tag": items = db.Tags.Select(x => new KeyValuePair<Guid, string>(x.Id, x.Name)); break;
                case "series": items = db.Series.Select(x => new KeyValuePair<Guid, string>(x.Id, x.Name)); break;
                case "source": case "library": items = db.Sources.Select(x => new KeyValuePair<Guid, string>(x.Id, x.Name)); break;
                case "feature": items = db.Features.Select(x => new KeyValuePair<Guid, string>(x.Id, x.Name)); break;
                case "agerating": items = db.AgeRatings.Select(x => new KeyValuePair<Guid, string>(x.Id, x.Name)); break;
                case "region": items = db.Regions.Select(x => new KeyValuePair<Guid, string>(x.Id, x.Name)); break;
                case "completionstatuses": items = db.CompletionStatuses.Select(x => new KeyValuePair<Guid, string>(x.Id, x.Name)); break;
                default: throw new InvalidOperationException("filter " + field + " cannot be set by name");
            }
            var all = items.ToList();
            var ids = new List<Guid>();
            foreach (var n in names)
            {
                var hit = all.FirstOrDefault(i => string.Equals(i.Value, n, StringComparison.OrdinalIgnoreCase));
                if (hit.Value == null) throw new InvalidOperationException("no " + field + " named " + n);
                ids.Add(hit.Key);
            }
            return ids;
        }

        private static string Describe(IPlayniteAPI api, object settings)
        {
            var sb = new StringBuilder();
            var main = Application.Current.MainWindow;
            if (main != null) sb.AppendLine("window=" + (int)main.ActualWidth + "x" + (int)main.ActualHeight + " state=" + main.WindowState);
            var view = settings.GetType().GetProperty("ViewSettings").GetValue(settings, null);
            sb.AppendLine("view=" + view.GetType().GetProperty("GamesViewType").GetValue(view, null));
            sb.AppendLine("explorer=" + settings.GetType().GetProperty("ExplorerPanelVisible").GetValue(settings, null));
            sb.AppendLine("filterpanel=" + settings.GetType().GetProperty("FilterPanelVisible").GetValue(settings, null));
            var selected = api.MainView.SelectedGames == null ? null : api.MainView.SelectedGames.FirstOrDefault();
            sb.AppendLine("selected=" + (selected == null ? "" : selected.Name));
            sb.AppendLine("games=" + api.MainView.FilteredGames.Count());
            var host = main == null ? null : FindAll<ScrollViewer>(main).FirstOrDefault(v => v.Name == "PART_ScrollViewHost" && v.IsVisible);
            if (host != null) sb.AppendLine("scrollable=" + (int)host.ScrollableHeight + " offset=" + (int)host.VerticalOffset);
            return sb.ToString();
        }
    }
}
