using System.IO;
using System.Linq;
using LayoutStudio.Core;

public static class TestPaths
{
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "layout-studio"))) dir = dir.Parent;
        if (dir == null) throw new System.Exception("repository root not found");
        return dir.FullName;
    }

    public static string SchemaPath()
    {
        string inRepo = Path.Combine(RepoRoot(), "theme", "Modula", "layout-studio.schema.json");
        // the exported Layout Studio repository ships a copy of the Modula schema as a test fixture
        return File.Exists(inRepo) ? inRepo : Path.Combine(RepoRoot(), "tests", "fixtures", "modula.layout-studio.schema.json");
    }
}

public static class SchemaTests
{
    private static Schema Load()
    {
        return Schema.Load(File.ReadAllText(TestPaths.SchemaPath()));
    }

    public static void Loads_converted_schema_with_seven_pages()
    {
        var s = Load();
        Assert.Equal(7, s.Pages.Count);
        Assert.Equal(7, s.Pages.Select(p => p.Id).Distinct().Count(), "page ids are unique");
    }

    public static void Constants_cover_the_known_options()
    {
        var s = Load();
        var all = s.AllConstants().ToList();
        Assert.True(all.Count >= 300, "at least 300 constants, got " + all.Count);
        Assert.Equal(all.Count, all.Select(c => c.Key).Distinct().Count(), "constant keys are unique");
        foreach (var key in new[] { "WindowGlassBlurRadius", "TopPanelGlassOpacity", "SidebarGlassOpacity", "HeaderTileSlot12", "DetailsViewSectionOrderNotes", "HeroCardMargin" })
            Assert.True(s.Find(key) != null, "missing constant " + key);
    }

    public static void Every_constant_has_a_default()
    {
        var s = Load();
        var missing = s.AllConstants().Where(c => string.IsNullOrEmpty(c.Default) && c.Type != ConstantType.String).Select(c => c.Key).ToList();
        Assert.Equal(0, missing.Count, "constants without default: " + string.Join(",", missing.Take(8)));
    }

    public static void Every_control_key_is_a_known_constant()
    {
        var s = Load();
        var unknown = s.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls)
            .SelectMany(c => c.Keys.Concat(c.Choices.SelectMany(ch => ch.Constants.Keys)))
            .Where(k => s.Find(k) == null).Distinct().ToList();
        Assert.Equal(0, unknown.Count, "unknown keys: " + string.Join(",", unknown.Take(8)));
    }

    public static void Tiles_are_one_ordered_list_of_12()
    {
        var s = Load();
        var tiles = s.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Where(c => c.Kind == "tileSlots").ToList();
        Assert.Equal(1, tiles.Count);
        Assert.Equal(12, tiles[0].Keys.Count);
        Assert.Equal("HeaderTileSlot1", tiles[0].Keys[0]);
        Assert.Equal("HeaderTileSlot12", tiles[0].Keys[11]);
        Assert.Equal(34, tiles[0].Choices.Count, "tile types without the Default alias");
    }

    public static void Section_orders_are_ordered_lists()
    {
        var s = Load();
        var lists = s.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Where(c => c.Kind == "orderedList" && c.Id != "HeroChipsOrder").ToList();
        Assert.Equal(3, lists.Count, "details left, details right, grid");
        Assert.Equal(11, lists[0].Keys.Count);
        Assert.Equal(11, lists[1].Keys.Count);
        Assert.Equal(lists.Sum(l => l.Keys.Count), lists.SelectMany(l => l.Keys).Distinct().Count(), "no key in two lists");
    }

    public static void Unknown_key_returns_null()
    {
        Assert.Equal(null, Load().Find("NoSuchConstant"));
    }

    public static void Preset_groups_keep_their_default_choice()
    {
        var s = Load();
        var presets = s.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).Where(c => c.Kind == "preset").ToList();
        Assert.True(presets.Count >= 20, "preset groups present");
        Assert.True(presets.All(c => c.Choices.Any(ch => ch.Id == "Default") || c.DefaultChoice != null), "every preset group has a default choice");
    }

    public static void Invalid_json_throws_a_clear_exception()
    {
        Assert.Throws<System.FormatException>(() => Schema.Load("{ not json"));
    }

    public static void Dependents_list_the_brushes_driven_by_a_constant()
    {
        var s = Load();
        var panel = s.DependentsOf("TopPanelGlassOpacity");
        Assert.True(panel.Any(d => d.Resource == "TopPanelBackgroundBrush" && d.Property == "Opacity"), "the top bar brush follows TopPanelGlassOpacity");
        Assert.True(s.DependentsOf("FilterPanelGlassOpacity").Any(d => d.Resource == "FilterPanelViewBackgroundBrush" && d.Property == "Opacity"));
        Assert.True(s.DependentsOf("SidebarGlassOpacity").Any(d => d.Resource == "SidebarBackgroundBrush" && d.Property == "Opacity"));
        Assert.True(s.DependentsOf("SectionGlassOpacity").Any(d => d.Resource == "SectionGlassBrush"));
        Assert.True(s.DependentsOf("TextColor").Any(d => d.Resource == "SectionGlassBorderBrush" && d.Property == "Color"), "Color dependencies are listed too");
        Assert.True(s.DependentsOf("SectionGlassBorderOpacity").Any(d => d.Resource == "GlassRimBrush" && d.Property == "Opacity"), "gradient brushes are rebuilt too");
        Assert.Equal(0, s.DependentsOf("NoSuchConstant").Count, "unknown constants have no dependents");
    }

    public static void Restart_keys_flag_their_constants_and_controls()
    {
        var s = Schema.Load(@"{""version"":1,""restartKeys"":[""Radius""],
          ""constants"":[{""key"":""Radius"",""type"":""Double"",""default"":""14""},{""key"":""Gap"",""type"":""Double"",""default"":""4""}],
          ""pages"":[{""id"":""p"",""title"":""P"",""groups"":[{""id"":""g"",""title"":""G"",""controls"":[
            {""kind"":""number"",""id"":""Radius"",""keys"":[""Radius""],""title"":""Radius"",""needsRestart"":true,""choices"":[]},
            {""kind"":""number"",""id"":""Gap"",""keys"":[""Gap""],""title"":""Gap"",""choices"":[]}]}]}]}");
        Assert.True(s.NeedsRestart("Radius"));
        Assert.False(s.NeedsRestart("Gap"));
        Assert.False(s.NeedsRestart("NoSuchConstant"));
        var controls = s.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).ToList();
        Assert.True(controls.Single(c => c.Id == "Radius").NeedsRestart, "the control carries the flag for the editor badge");
        Assert.False(controls.Single(c => c.Id == "Gap").NeedsRestart);
    }

    public static void Every_constant_of_the_theme_is_live_through_resources_or_the_options_bridge()
    {
        var s = Load();
        Assert.False(s.NeedsRestart("TopPanelHeight"), "read with StaticResource only as a fallback of the Options bridge binding");
        Assert.False(s.NeedsRestart("DetailsViewGenresInRightColumn"));
        Assert.False(s.NeedsRestart("WindowGlassBlurRadius"));
    }

    public static void Numeric_constants_of_the_theme_have_bounds_WPF_accepts()
    {
        var s = Load();
        foreach (var def in s.AllConstants())
        {
            if (def.Type == ConstantType.Double && double.Parse(def.Default, System.Globalization.CultureInfo.InvariantCulture) >= 0)
                Assert.True(def.Min.HasValue && def.Min.Value >= (def.Key.EndsWith("FontSize") ? 1 : 0), def.Key + " needs a minimum (a negative size, radius or font size makes WPF throw)");
            if ((def.Type == ConstantType.Thickness || def.Type == ConstantType.CornerRadius) && !def.Key.Contains("Margin"))
                Assert.True(def.Min.HasValue && def.Min.Value >= 0, def.Key + " must not accept negative numbers (borders and paddings throw)");
        }
    }

    public static void Every_size_says_what_it_is_and_where_it_lives()
    {
        var s = Load();
        Assert.True(s.Sizes.Count >= 11);
        var groups = s.SizeGroups.Select(g => g.Id).ToList();
        Assert.True(groups.Count >= 3, "the groups are in the schema");
        foreach (var size in s.Sizes)
        {
            Assert.True(!string.IsNullOrEmpty(size.Description), size.Id + " has a description");
            Assert.True(groups.Contains(size.Group), size.Id + " is in a known group");
            Assert.True(!string.IsNullOrEmpty(size.WhereLabel), size.Id + " says where it is");
            Assert.Equal(4, size.Rect.Length, size.Id + " has a rect");
            foreach (var v in size.Rect) Assert.True(v >= 0 && v <= 1, size.Id + " rect is a fraction of the window");
            Assert.True(size.Rect[0] + size.Rect[2] <= 1.0001 && size.Rect[1] + size.Rect[3] <= 1.0001, size.Id + " stays inside the window");
        }
    }

    public static void Cover_has_a_locked_two_by_three_ratio_and_the_search_box_has_none()
    {
        var s = Load();
        var cover = s.Sizes.Single(z => z.Id == "cover");
        Assert.True(cover.HasRatio);
        Assert.Equal("2:3", cover.RatioDefault);
        Assert.True(cover.RatioLocked);
        Assert.False(s.Sizes.Single(z => z.Id == "searchBox").HasRatio);
    }

    public static void Tab_numbers_are_choices_and_the_six_chip_orders_are_one_ordered_list()
    {
        var s = Load();
        var all = s.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).ToList();
        var tab = all.Single(c => c.Id == "DetailsViewTabNotes");
        Assert.Equal("choice", tab.Kind);
        Assert.Equal("0:Every tab,1:About,2:Media,3:Stats,4:Community", string.Join(",", tab.Choices.Select(c => c.Id + ":" + c.Name)));
        Assert.False(all.Any(c => c.Id.StartsWith("DetailsViewTab") && c.Id != "DetailsViewTabsMode" && c.Kind == "slider"), "no tab slider is left");
        var order = all.Single(c => c.Id == "HeroChipsOrder");
        Assert.Equal("orderedList", order.Kind);
        Assert.Equal(6, order.Keys.Count);
        Assert.False(all.Any(c => c.Kind == "slider" && c.Id.StartsWith("HeroChipsOrder") && c.Id != "HeroChipsOrder"), "the six order sliders are gone");
    }
    public static void Real_schema_passes_validation()
    {
        var problems = Load().Validate();
        Assert.Equal(0, problems.Count, string.Join("; ", problems));
    }

    public static void Validation_reports_a_section_with_a_column_key_but_no_order_keys()
    {
        var s = Schema.Load(@"{ ""version"":1, ""constants"":[{""key"":""InRight"",""type"":""Boolean"",""default"":""False""}],
            ""pages"":[], ""sections"":[{""id"":""genres"",""title"":""Genres"",""columnKey"":""InRight""}] }");
        var problems = s.Validate();
        Assert.True(problems.Any(p => p.Contains("genres")), "the section is named: " + string.Join("; ", problems));
    }

    public static void Validation_reports_a_key_the_constants_do_not_define()
    {
        var s = Schema.Load(@"{ ""version"":1, ""constants"":[], ""pages"":[], ""corners"":[{""key"":""Ghost"",""title"":""G"",""round"":10}] }");
        Assert.True(s.Validate().Any(p => p.Contains("Ghost")));
    }
}
