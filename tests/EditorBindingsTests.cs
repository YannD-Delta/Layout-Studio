using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using LayoutStudio.Core;
using LayoutStudio.Editor;

/// <summary>The editor XAML has no code-behind: a binding to a name no view model defines fails silently (a button that does nothing).</summary>
public static class EditorBindingsTests
{
    private static string Xaml()
    {
        return File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), @"layout-studio\src\Editor\EditorWindow.xaml"));
    }

    private static string[] ViewModelMembers()
    {
        return typeof(ViewModelBase).Assembly.GetTypes()
            .Where(t => t.IsClass && t.Namespace != null && t.Namespace.StartsWith("LayoutStudio"))
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            .Where(m => m.MemberType == MemberTypes.Property || m.MemberType == MemberTypes.Field)
            .Select(m => m.Name).Distinct().ToArray();
    }

    // properties of WPF elements, reached with RelativeSource or ElementName bindings
    private static readonly string[] ElementProperties = { "DataContext", "Foreground", "Tag", "ViewportHeight", "ActualWidth", "ActualHeight", "PlacementTarget", "IsKeyboardFocused", "IsMouseOver", "Converter", "ElementName" };

    public static void Every_command_binding_of_the_editor_names_a_command_some_view_model_defines()
    {
        var members = ViewModelMembers();
        var missing = Regex.Matches(Xaml(), @"Command=""\{Binding (?:Path=)?([A-Za-z_][A-Za-z0-9_.]*)")
            .Cast<Match>().Select(m => m.Groups[1].Value).Select(n => n.Substring(n.LastIndexOf('.') + 1)).Distinct()
            .Where(n => !members.Contains(n)).ToArray();
        Assert.True(missing.Length == 0, "buttons bound to commands nobody defines: " + string.Join(", ", missing));
    }

    public static void Every_plain_binding_of_the_editor_names_a_member_some_view_model_defines()
    {
        var members = ViewModelMembers();
        // Self, ., Parent paths and attached properties are not view model members
        var missing = Regex.Matches(Xaml(), @"\{Binding (?:Path=)?([A-Za-z_][A-Za-z0-9_.]*)")
            .Cast<Match>().Select(m => m.Groups[1].Value).Select(n => n.Substring(n.LastIndexOf('.') + 1)).Distinct()
            .Where(n => !members.Contains(n) && !ElementProperties.Contains(n))
            .ToArray();
        Assert.True(missing.Length == 0, "bindings to members nobody defines: " + string.Join(", ", missing));
    }
}
