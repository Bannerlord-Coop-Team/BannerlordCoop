using Common.Logging;
using Common.Util;
using E2E.Tests.Util;
using GameInterface.Services.MapTracks.Data;
using GameInterface.Services.MapTracks.Interfaces;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapTracks;

public class MapTrackFactionTests : SyncTestBase
{
    public MapTrackFactionTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ApplyVisibleClanOrKingdomTrack_ResolvesFactionWithoutCastErrors(bool kingdom, bool compact)
    {
        var clanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var kingdomId = kingdom ? TestEnvironment.CreateRegisteredObject<Kingdom>() : null;
        var logs = new ConcurrentQueue<string>();
        Action<string> capture = logs.Enqueue;
        OutputSinkManager.AddLogCallback(capture);
        try
        {
            foreach (var client in Clients)
            {
                client.Call(() =>
                {
                    Assert.True(client.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
                    using (new AllowedThread())
                    {
                        Hero.MainHero.Clan = clan;
                        if (kingdom)
                        {
                            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var faction));
                            clan.Kingdom = faction;
                        }
                    }
                    Assert.NotNull(Hero.MainHero.MapFaction);
                    var factionId = kingdom ? kingdomId! : clanId;
                    var prefix = kingdom ? "Kingdom_" : "Clan_";
                    if (compact)
                    {
                        Assert.StartsWith(prefix, factionId);
                        factionId = factionId.Substring(prefix.Length);
                    }
                    var track = new Track { IsPointer = true, IsEnemy = true };
                    var behavior = new MapTracksCampaignBehavior();
                    var tracks = client.Resolve<IMapTracksCampaignBehaviorInterface>();

                    tracks.ApplyVisibleTrackChanges(behavior, new List<MapTrackData>
                    {
                        new MapTrackData(track, factionId)
                    }, false);

                    Assert.Contains(track, behavior._detectedTracksCache);
                    Assert.True(track.IsDetected);
                    Assert.False(track.IsEnemy);
                });
            }
            Assert.DoesNotContain(logs, line => line.Contains("Could not cast") || line.Contains("Failed to get"));
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(capture);
        }
    }
}
