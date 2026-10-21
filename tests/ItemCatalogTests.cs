using System.IO;
using System.Linq;
using LayoutStudio.Core;

/// <summary>The list of items is generated from the theme's options: nothing the game page can show is left without a row, and no option has two homes.</summary>
public static class ItemCatalogTests
{
    private static Schema Schema_() { return Schema.Load(File.ReadAllText(TestPaths.SchemaPath())); }

    private static readonly string[] CardPrefixes = { "HeroShow", "HeroChip", "HeaderTile", "HeaderRow", "HeroInfoLine", "HeroPlayersCount", "HeroStyleShow" };

    public static void Every_option_of_a_chip_or_a_header_card_belongs_to_exactly_one_item_or_place()
    {
        var s = Schema_();
        var catalog = ItemCatalog.Build(s);
        var keys = s.AllConstants().Select(c => c.Key).Where(k => CardPrefixes.Any(p => k.StartsWith(p, System.StringComparison.Ordinal))).ToList();
        Assert.True(keys.Count > 100, "the schema has the chip and card options: " + keys.Count);
        var orphans = keys.Where(k => ItemCatalog.OwnersOf(catalog, k).Count == 0).ToList();
        Assert.True(orphans.Count == 0, "options with no item and no place: " + string.Join(", ", orphans));
        var shared = keys.Where(k => ItemCatalog.OwnersOf(catalog, k).Count > 1).Select(k => k + " (" + string.Join(" + ", ItemCatalog.OwnersOf(catalog, k)) + ")").ToList();
        Assert.True(shared.Count == 0, "options with two homes: " + string.Join(", ", shared));
    }

    public static void Every_header_card_type_belongs_to_an_item_that_has_a_row()
    {
        var s = Schema_();
        var catalog = ItemCatalog.Build(s);
        var tiles = s.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).First(c => c.Kind == "tileSlots").Choices.Where(c => c.Id != "Empty").ToList();
        Assert.Equal(33, tiles.Count);
        foreach (var tile in tiles)
        {
            var owners = catalog.Where(i => i.TileIds.Contains(tile.Id)).ToList();
            Assert.Equal(1, owners.Count, "tile type " + tile.Id + " belongs to exactly one item");
            Assert.Equal(ItemCatalog.TileItemId(tile.Id), owners[0].Id);
        }
    }

    public static void The_scores_the_cards_show_are_items_with_their_own_card_types()
    {
        var catalog = ItemCatalog.Build(Schema_());
        Assert.Equal("Scores,CommunityScore", string.Join(",", catalog.Single(i => i.Id == "CommunityScore").TileIds));
        Assert.Equal("CriticScore", string.Join(",", catalog.Single(i => i.Id == "CriticScore").TileIds));
        Assert.Equal("UserScore", string.Join(",", catalog.Single(i => i.Id == "UserScore").TileIds));
        Assert.Equal("PlayTime", string.Join(",", catalog.Single(i => i.Id == "PlayTime").TileIds));
        Assert.Equal("HltbMain,HltbMainExtra,HltbCompletionist,HltbAll", string.Join(",", catalog.Single(i => i.Id == "Hltb").TileIds));
        Assert.True(catalog.Single(i => i.Id == "InstallSize").InPanel, "install size is a row of the information panel too");
    }

    public static void Every_chip_has_its_show_switch_its_break_switch_and_its_place_in_the_line()
    {
        var s = Schema_();
        var chips = ItemCatalog.Build(s).Where(i => i.InChips).ToList();
        Assert.Equal(17, chips.Count);
        foreach (var chip in chips)
        {
            Assert.True(chip.ChipOrderKey != null, chip.Id + " has an order constant");
            Assert.True(chip.ChipBreakKeys.Count >= 1, chip.Id + " has a new line switch");
        }
        Assert.Equal(3, chips.Single(i => i.Id == "Hltb").ChipBreakKeys.Count);
        Assert.Equal(string.Join(",", ChipLayout.Ids(s).OrderBy(x => x)), string.Join(",", chips.Select(i => i.Id).OrderBy(x => x)));
    }

    public static void The_sections_and_the_information_panel_rows_are_found_by_item()
    {
        var catalog = ItemCatalog.Build(Schema_());
        Assert.Equal(19, catalog.Count(i => i.InSection), "one item per section");
        Assert.Equal(17, catalog.Sum(i => i.PanelKeys.Count), "seventeen rows in the information panel, each with its switch");
        Assert.Equal("ReleaseDate,Platforms,Source,Developers,CommunityScore,RecentActivity,InstallSize,Added,Publishers,Series,AgeRatings,Version,UserScore,CriticScore,Library,InstallDirectory,Regions", string.Join(",", catalog.Where(i => i.InPanel).Select(i => i.Id)));
        Assert.True(catalog.Single(i => i.Id == "Hltb").InSection && catalog.Single(i => i.Id == "Hltb").InChips && catalog.Single(i => i.Id == "Hltb").InCards,
            "HowLongToBeat shows as a chip, a card and a section");
    }

    public static void The_places_agree_with_the_information_shown_once_table()
    {
        var s = Schema_();
        var catalog = ItemCatalog.Build(s);
        var tiles = s.Pages.SelectMany(p => p.Groups).SelectMany(g => g.Controls).First(c => c.Kind == "tileSlots").Choices;
        foreach (var info in InfoOnce.All)
        {
            var item = catalog.Single(i => i.Id == info.Id);
            Assert.Equal(info.ChipKey, item.ChipShowKey, info.Id + " chip");
            foreach (var value in info.TileValues ?? new int[0])
            {
                var tile = tiles.Single(t => t.Value == value.ToString());
                Assert.Equal(info.Id, ItemCatalog.TileItemId(tile.Id), info.Id + " card type " + tile.Id);
            }
            if (info.SectionShowKey != null) Assert.True(item.InSection && item.Section.ShowKey == info.SectionShowKey, info.Id + " section");
            Assert.Equal(info.InPanel, item.InPanel, info.Id + " panel");
        }
    }

    public static void Places_of_the_game_page_are_listed_top_to_bottom()
    {
        Assert.Equal("chips,cards,widgets,panel,sections,elsewhere", string.Join(",", ItemZones.All));
        Assert.Equal("chips", ItemZones.ZoneOf("HeroShowGenres"));
        Assert.Equal("chips", ItemZones.ZoneOf("HeroChipBreakHltbMain"));
        Assert.Equal("chips", ItemZones.ZoneOf("HeroChipOrderTags"));
        Assert.Equal("panel", ItemZones.ZoneOf("InfoPanelShowInstallSize"));
        Assert.Equal("sections", ItemZones.ZoneOf("DetailsViewExpandGenres"));
        Assert.Equal("elsewhere", ItemZones.ZoneOf("GridViewShowNotesPanel"));
        Assert.Equal("elsewhere", ItemZones.ZoneOf("HltbShowOnCover"));
        Assert.Equal("widgets", ItemZones.ZoneOf("HeroShowMusicPlayer"));
        Assert.Equal("widgets", ItemZones.ZoneOf("DetailsViewShowUserRating"));
        Assert.Equal(null, ItemZones.ZoneOf("HeroChipPadding"), "the look of the chips belongs to the chips line, not to an item");
        Assert.Equal("chips", ItemZones.ZoneOwning("HeroChipPadding"));
        Assert.Equal("cards", ItemZones.ZoneOwning("HeaderTileSlot7C"));
    }
}
