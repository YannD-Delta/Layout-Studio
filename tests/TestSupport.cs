using System.Collections.Generic;
using System.IO;
using LayoutStudio.Core;

public class FakePresetFiles : IPresetFiles
{
    public List<string> Calls = new List<string>();
    public void Merge(string groupId, string absolutePath) { Calls.Add("merge " + groupId + " " + Path.GetFileName(absolutePath)); }
    public void Remove(string groupId) { Calls.Add("remove " + groupId); }
}

public static class TestSchema
{
    // A small schema with one control of every kind, spread over two pages.
    public const string Json = @"{
  ""version"": 1,
  ""constants"": [
    {""key"":""WindowGlassEnabled"",""type"":""Boolean"",""default"":""True""},
    {""key"":""WindowGlassBlurRadius"",""type"":""Double"",""default"":""30"",""min"":0,""max"":80,""step"":1},
    {""key"":""SlotCount"",""type"":""Int"",""default"":""4"",""min"":1,""max"":12},
    {""key"":""Accent"",""type"":""Color"",""default"":""#FF1E90FF""},
    {""key"":""PageSideMargin"",""type"":""Thickness"",""default"":""20,0,20,0""},
    {""key"":""CoverSubtitleVisibility"",""type"":""Visibility"",""default"":""Collapsed""},
    {""key"":""Radius"",""type"":""Double"",""default"":""14""},
    {""key"":""OrderNotes"",""type"":""Int"",""default"":""0"",""min"":0,""max"":2},
    {""key"":""OrderGenres"",""type"":""Int"",""default"":""1"",""min"":0,""max"":2},
    {""key"":""OrderTags"",""type"":""Int"",""default"":""2"",""min"":0,""max"":2},
    {""key"":""HeaderTileSlot1"",""type"":""Int"",""default"":""1""},
    {""key"":""HeaderTileSlot2"",""type"":""Int"",""default"":""2""}
  ],
  ""pages"": [
    {""id"":""background"",""title"":""Background and glass"",""groups"":[
      {""id"":""window"",""title"":""Window background"",""controls"":[
        {""kind"":""toggle"",""id"":""WindowGlassEnabled"",""keys"":[""WindowGlassEnabled""],""title"":""Show the game artwork behind the window"",""titleKey"":""LOCT_Glass"",""choices"":[]},
        {""kind"":""slider"",""id"":""WindowGlassBlurRadius"",""keys"":[""WindowGlassBlurRadius""],""title"":""Window background blur"",""choices"":[]}]},
      {""id"":""corners"",""title"":""Corners"",""controls"":[
        {""kind"":""preset"",""id"":""Corners"",""keys"":[],""title"":""Corner rounding"",""defaultChoice"":""Default"",""choices"":[
          {""id"":""Default"",""name"":""Default"",""constants"":{}},
          {""id"":""Sharp"",""name"":""Sharp"",""constants"":{""Radius"":""0""}}]}]}]},
    {""id"":""details"",""title"":""Details view"",""groups"":[
      {""id"":""misc"",""title"":""Misc"",""controls"":[
        {""kind"":""number"",""id"":""SlotCount"",""keys"":[""SlotCount""],""title"":""Number of slots"",""choices"":[]},
        {""kind"":""color"",""id"":""Accent"",""keys"":[""Accent""],""title"":""Accent color"",""choices"":[]},
        {""kind"":""thickness"",""id"":""PageSideMargin"",""keys"":[""PageSideMargin""],""title"":""Page side margin"",""choices"":[]},
        {""kind"":""choice"",""id"":""CoverSubtitleVisibility"",""keys"":[""CoverSubtitleVisibility""],""title"":""Subtitle under covers"",""choices"":[
          {""id"":""Visible"",""name"":""Visible"",""constants"":{}},{""id"":""Collapsed"",""name"":""Hidden"",""constants"":{}}]}]},
      {""id"":""order"",""title"":""Section order"",""controls"":[
        {""kind"":""orderedList"",""id"":""Order"",""keys"":[""OrderNotes"",""OrderGenres"",""OrderTags""],""title"":""Sections"",""choices"":[]}]},
      {""id"":""tiles"",""title"":""Header tiles"",""controls"":[
        {""kind"":""tileSlots"",""id"":""HeaderTiles"",""keys"":[""HeaderTileSlot1"",""HeaderTileSlot2""],""title"":""Header tiles"",""choices"":[
          {""id"":""Empty"",""name"":""Empty"",""value"":""0"",""constants"":{}},
          {""id"":""PlayTime"",""name"":""Time played"",""value"":""1"",""constants"":{}},
          {""id"":""LastPlayed"",""name"":""Last played"",""value"":""2"",""constants"":{}}]}]}]}
  ]
}";
}

public static class TestSections
{
    /// <summary>The theme's schema with the column flag of some sections removed: those sections stay in their column (the "fixed" case of the editor).</summary>
    public static string SchemaJsonWithFixed(params string[] ids)
    {
        var serializer = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };
        var root = (System.Collections.Generic.Dictionary<string, object>)serializer.DeserializeObject(System.IO.File.ReadAllText(TestPaths.SchemaPath()));
        var defaults = new System.Collections.Generic.Dictionary<string, string>();
        foreach (var c in (System.Collections.IEnumerable)root["constants"])
        {
            var cd = (System.Collections.Generic.Dictionary<string, object>)c;
            defaults[(string)cd["key"]] = cd.ContainsKey("default") ? (string)cd["default"] : null;
        }
        foreach (var section in (System.Collections.IEnumerable)root["sections"])
        {
            var d = (System.Collections.Generic.Dictionary<string, object>)section;
            if (System.Array.IndexOf(ids, (string)d["id"]) < 0) continue;
            // a fixed section only knows the column it is in: drop the flag and the order constant of the other column
            bool right = d.ContainsKey("columnKey") && defaults[(string)d["columnKey"]] == "True";
            d.Remove("columnKey");
            d.Remove(right ? "leftKey" : "rightKey");
        }
        return serializer.Serialize(root);
    }
}

/// <summary>Finds the pieces of the ten-page editor: a page by id, a block by id, the editor of a kind.</summary>
public static class ShellFinder
{
    public static LayoutStudio.Editor.PageViewModel PageOf(this LayoutStudio.Editor.ShellViewModel shell, string id)
    {
        return System.Linq.Enumerable.Single(shell.Pages, p => p.Id == id);
    }

    public static LayoutStudio.Editor.GroupViewModel Block(this LayoutStudio.Editor.ShellViewModel shell, string blockId)
    {
        return System.Linq.Enumerable.Single(System.Linq.Enumerable.SelectMany(shell.Pages, p => p.Groups), g => g.Id == blockId);
    }

    public static T EditorOf<T>(this LayoutStudio.Editor.ShellViewModel shell) where T : class
    {
        return System.Linq.Enumerable.Single(System.Linq.Enumerable.OfType<T>(System.Linq.Enumerable.Select(System.Linq.Enumerable.SelectMany(shell.Pages, p => p.Groups), g => g.Editor)));
    }

    public static System.Collections.Generic.IEnumerable<LayoutStudio.Editor.SettingViewModel> AllSettings(this LayoutStudio.Editor.ShellViewModel shell)
    {
        return System.Linq.Enumerable.SelectMany(System.Linq.Enumerable.SelectMany(shell.Pages, p => p.Groups), g => g.Settings);
    }
}
