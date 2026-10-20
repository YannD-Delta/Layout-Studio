using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LayoutStudio.Editor
{
    /// <summary>Picks the DataTemplate "Template.&lt;kind&gt;" of the editor window for a setting view model.</summary>
    public class KindTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var element = container as FrameworkElement;
            var setting = item as SettingViewModel;
            if (element == null || setting == null) return null;
            string key = "Template." + setting.TemplateName;
            return (element.TryFindResource(key) ?? element.TryFindResource("Template.unsupported")) as DataTemplate;
        }
    }

    /// <summary>Picks the schematic "Glyph.&lt;id&gt;" of a setting that has one; nothing for the others (the glyph slot stays empty and takes no room).</summary>
    public class GlyphTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var element = container as FrameworkElement;
            var setting = item as SettingViewModel;
            if (element == null || setting == null || !setting.HasGlyph) return null;
            return element.TryFindResource("Glyph." + setting.GlyphKey) as DataTemplate;
        }
    }

    /// <summary>Picks the scene "Scene.<size id>" the Sizes preview dock draws for a size (the plain box when it has none).</summary>
    public class SceneTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var element = container as FrameworkElement;
            var size = item as SizeItemViewModel;
            if (element == null || size == null) return null;
            return (element.TryFindResource("Scene." + size.Id) ?? element.TryFindResource("Scene.box")) as DataTemplate;
        }
    }

    /// <summary>Picks the content template "Page.&lt;kind&gt;" for a page (generated settings or a visual editor).</summary>
    public class PageContentSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var element = container as FrameworkElement;
            var page = item as PageViewModel;
            if (element == null || page == null) return null;
            return (element.TryFindResource("Page." + page.Kind) ?? element.TryFindResource("Page.settings")) as DataTemplate;
        }
    }

    /// <summary>File path to a bitmap decoded small (the preview is only a few hundred pixels wide); null when it cannot be read.</summary>
    public class PathToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var path = value as string;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var image = new System.Windows.Media.Imaging.BitmapImage();
                image.BeginInit();
                image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 640;
                image.UriSource = new Uri(path);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception) { return null; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// A preset choice to the part of its picture that differs from its sibling choices (see <see cref="LayoutStudio.Core.PreviewDiff"/>):
    /// the previews of a group are one screenshot with one setting changed, so only the part that moves is worth showing.
    /// Null when nothing differs or the choice has no picture; the whole picture when the group's pictures cannot be compared.
    /// </summary>
    public class ChoiceCropConverter : IValueConverter
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, System.Collections.Generic.Dictionary<ChoiceViewModel, ImageSource>> cache
            = new System.Runtime.CompilerServices.ConditionalWeakTable<object, System.Collections.Generic.Dictionary<ChoiceViewModel, ImageSource>>();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var choice = value as ChoiceViewModel;
            if (choice == null || choice.PreviewPath == null) return null;
            try
            {
                var crops = cache.GetValue(choice.Siblings ?? (object)choice, key => Compute(choice.Siblings ?? new System.Collections.Generic.List<ChoiceViewModel> { choice }));
                ImageSource image;
                return crops.TryGetValue(choice, out image) ? image : null;
            }
            catch (Exception) { return null; }
        }

        private static System.Collections.Generic.Dictionary<ChoiceViewModel, ImageSource> Compute(System.Collections.Generic.IList<ChoiceViewModel> siblings)
        {
            var result = new System.Collections.Generic.Dictionary<ChoiceViewModel, ImageSource>();
            var withPicture = siblings.Where(c => c.PreviewPath != null).ToList();
            var bitmaps = withPicture.Select(c => Load(c.PreviewPath)).ToList();
            if (bitmaps.Count == 0) return result;
            bool comparable = bitmaps.Count > 1 && bitmaps.All(b => b != null && b.PixelWidth == bitmaps[0].PixelWidth && b.PixelHeight == bitmaps[0].PixelHeight);
            if (!comparable)
            {
                for (int i = 0; i < withPicture.Count; i++) result[withPicture[i]] = bitmaps[i];   // one picture, or sizes that differ: show it whole
                return result;
            }
            int w = bitmaps[0].PixelWidth, h = bitmaps[0].PixelHeight;
            var frames = bitmaps.Select(b =>
            {
                var pixels = new byte[w * h * 4];
                new FormatConvertedBitmap(b, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, w * 4, 0);
                return pixels;
            }).ToList();
            var box = LayoutStudio.Core.PreviewDiff.Box(frames, w, h);
            if (box.IsEmpty) return result;   // the pictures show no difference: nothing to show
            for (int i = 0; i < withPicture.Count; i++)
            {
                var crop = new CroppedBitmap(bitmaps[i], new Int32Rect(box.X, box.Y, box.Width, box.Height));
                crop.Freeze();
                result[withPicture[i]] = crop;
            }
            return result;
        }

        private static BitmapSource Load(string path)
        {
            try
            {
                var image = new System.Windows.Media.Imaging.BitmapImage();
                image.BeginInit();
                image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception) { return null; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class ColorTextToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString((string)value)); }
            catch (Exception) { return Brushes.Transparent; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>Loads the editor window from the XAML embedded in the plugin assembly. No code-behind: everything binds to view models.</summary>
    public static class EditorWindowFactory
    {
        public static Window Create(ShellViewModel shell, string language)
        {
            var window = (Window)LoadXaml("LayoutStudio.Editor.EditorWindow.xaml");
            var strings = (ResourceDictionary)LoadXaml("LayoutStudio.Localization." + (string.Equals(language, "fr_FR", StringComparison.OrdinalIgnoreCase) ? "fr_FR" : "en_US") + ".xaml");
            window.Resources.MergedDictionaries.Add(strings);
            // the view models word their own texts (advice page, banner) from the same dictionary
            if (shell.PlayniteSettings != null) shell.PlayniteSettings.Ui.Lookup = key => strings.Contains(key) ? strings[key] as string : null;
            window.DataContext = shell;
            // the Sizes page marks the sizes that are on screen: ask Playnite what it shows when the editor gets the focus and every few seconds
            window.Activated += (s, e) => shell.RefreshLive();
            var liveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            liveTimer.Tick += (s, e) => shell.RefreshLive();
            window.Loaded += (s, e) => liveTimer.Start();
            window.Closed += (s, e) => liveTimer.Stop();
            // A slider drag is one undo step: group everything between the thumb's drag start and completion.
            window.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((s, e) => Gesture(e.OriginalSource, true)));
            window.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((s, e) => Gesture(e.OriginalSource, false)));
            // A thumb tagged resize-w / resize-h / resize-wh drags a miniature box along that axis: the box view model turns the movement into constants.
            window.AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler((s, e) =>
            {
                var thumb = e.OriginalSource as Thumb;
                var item = thumb == null ? null : thumb.DataContext as SizeItemViewModel;
                string axis = ResizeAxis(thumb);
                if (item != null && axis != null) item.Drag(axis, e.HorizontalChange, e.VerticalChange, Modifiers());
            }));
            // The card that was clicked, focused or dragged becomes the active one.
            window.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((s, e) => ActivateCardOf(e.OriginalSource as DependencyObject)), true);
            window.AddHandler(UIElement.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((s, e) => ActivateCardOf(e.NewFocus as DependencyObject)), true);
            // Enter in a value box commits it at once (the boxes otherwise wait for focus to leave), Escape puts the old value back.
            window.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler((s, e) =>
            {
                var box = e.OriginalSource as TextBox;
                // a side box of a margin or corner editor: Up and Down step the number (Shift = 10, Ctrl = 0.5)
                if (box != null && (e.Key == Key.Up || e.Key == Key.Down) && (box.Tag as string) == "side")
                {
                    double number;
                    if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out number))
                    {
                        double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10 : (Keyboard.Modifiers & ModifierKeys.Control) != 0 ? 0.5 : 1;
                        box.Text = Math.Round(number + (e.Key == Key.Up ? step : -step), 4).ToString("R", CultureInfo.CurrentCulture);
                        var sideBinding = System.Windows.Data.BindingOperations.GetBindingExpression(box, TextBox.TextProperty);
                        if (sideBinding != null) sideBinding.UpdateSource();
                        box.CaretIndex = box.Text.Length;
                        e.Handled = true;
                    }
                    return;
                }
                if (box == null || box.AcceptsReturn || (e.Key != Key.Enter && e.Key != Key.Escape)) return;
                if (box.Name == "SearchBox") { if (e.Key == Key.Escape) { box.Clear(); e.Handled = true; } return; }
                var binding = System.Windows.Data.BindingOperations.GetBindingExpression(box, TextBox.TextProperty);
                if (binding == null) return;
                if (e.Key == Key.Enter) binding.UpdateSource(); else binding.UpdateTarget();
                e.Handled = true;
            }));
            // Arrow keys on a focused handle: one step along its axis (Shift = ten).
            window.PreviewKeyDown += (s, e) =>
            {
                var thumb = Keyboard.FocusedElement as Thumb;
                string axis = ResizeAxis(thumb);
                var item = thumb == null ? null : thumb.DataContext as SizeItemViewModel;
                if (item == null || axis == null) return;
                string along = null; int sign = 0;
                if (e.Key == Key.Right) { along = "w"; sign = 1; }
                else if (e.Key == Key.Left) { along = "w"; sign = -1; }
                else if (e.Key == Key.Down) { along = "h"; sign = 1; }
                else if (e.Key == Key.Up) { along = "h"; sign = -1; }
                if (along == null || !axis.Contains(along)) return;
                item.Nudge(along, sign, Modifiers());
                e.Handled = true;
            };
            return window;
        }

        private static string ResizeAxis(Thumb thumb)
        {
            var tag = thumb == null ? null : thumb.Tag as string;
            return tag != null && tag.StartsWith("resize-", StringComparison.Ordinal) ? tag.Substring(7) : null;
        }

        private static DragModifiers Modifiers()
        {
            var mods = DragModifiers.None;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods |= DragModifiers.Shift;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods |= DragModifiers.Ctrl;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods |= DragModifiers.Alt;
            return mods;
        }

        private static void ActivateCardOf(DependencyObject source)
        {
            for (var node = source; node != null; node = (node is Visual || node is System.Windows.Media.Media3D.Visual3D) ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            {
                var fe = node as FrameworkElement;
                var item = fe == null ? null : fe.DataContext as SizeItemViewModel;
                if (item != null) { item.Activate(); return; }
            }
        }

        private static void Gesture(object source, bool begin)
        {
            var target = (source as FrameworkElement) == null ? null : ((FrameworkElement)source).DataContext as IGestureTarget;
            if (target == null) return;
            if (begin) target.BeginGesture(); else target.EndGesture();
        }

        private static object LoadXaml(string resourceName)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null) throw new FileNotFoundException("embedded resource not found: " + resourceName);
                return XamlReader.Load(stream);
            }
        }
    }
}
