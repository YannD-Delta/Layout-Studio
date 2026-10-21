using System;
using LayoutStudio.Core;

public static class ActivitySummaryTests
{
    private static readonly DateTime Now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private const string Json = @"{""Items"":[
      {""DateSession"":""2026-10-18T20:00:00Z"",""ElapsedSeconds"":4320,""Details"":[]},
      {""DateSession"":""2026-09-01T20:00:00Z"",""ElapsedSeconds"":3600,""Details"":[]},
      {""DateSession"":""2026-10-10T08:00:00Z"",""ElapsedSeconds"":600,""Details"":[]}]}";

    public static void The_last_session_is_the_newest_one_and_recent_activity_counts_the_last_30_days()
    {
        var s = ActivitySummary.FromJson(Json, Now);
        Assert.Equal(3, s.Sessions);
        Assert.Equal(4320.0, s.LastSessionSeconds, "the 18th is the newest");
        Assert.Equal(2, s.RecentSessions, "the September session is older than 30 days");
        Assert.Equal(4920.0, s.RecentSeconds);
        Assert.True(s.HasData);
    }

    public static void No_data_or_broken_data_gives_an_empty_summary()
    {
        Assert.False(ActivitySummary.FromJson(null, Now).HasData);
        Assert.False(ActivitySummary.FromJson("{ nope", Now).HasData);
        Assert.False(ActivitySummary.FromJson(@"{""Items"":[]}", Now).HasData);
        Assert.False(ActivitySummary.FromJson(@"{""Items"":[{""DateSession"":""not a date""}]}", Now).HasData, "a session without a date is not counted");
    }

    public static void Durations_are_written_like_the_theme_writes_play_time()
    {
        Assert.Equal("1h 12m", ActivitySummary.Duration(4320));
        Assert.Equal("45m", ActivitySummary.Duration(2700));
        Assert.Equal("30s", ActivitySummary.Duration(30));
        Assert.Equal(null, ActivitySummary.Duration(0));
    }
}
