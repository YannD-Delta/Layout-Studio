using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

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
            string key = "Template." + SettingViewModel.TemplateKind(setting.Kind);
            return (element.TryFindResource(key) ?? element.TryFindResource("Template.unsupported")) as DataTemplate;
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
            window.DataContext = shell;
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
