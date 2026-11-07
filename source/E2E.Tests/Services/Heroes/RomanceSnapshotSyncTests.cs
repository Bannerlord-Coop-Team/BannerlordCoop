using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.HeirSelection.Interfaces;
using GameInterface.Services.Heroes.Messages.RomanceFlow;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.CommentBehaviors;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

/// <summary>Covers applying the server's romance snapshot on a client with the game's real event listener.</summary>
public class RomanceSnapshotSyncTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    // test heroes have no encyclopedia data, which the retirement log text needs
    private static readonly MethodBase[] TextMethods =
    {
        AccessTools.Method(typeof(ApplyHeirSelectionActionInterface), "RecordRetirement"),
    };

    public RomanceSnapshotSyncTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    // A retired hero whose clan closed has no Clan, which vanilla's romance listener doesn't expect.
    [Fact]
    public void Snapshot_WithRetiredClanlessHero_KeepsLaterStatesAndNotifications()
    {
        var client = environment.Clients.First();
        var closingClanId = CreateKingdomClan();
        var partnerClanId = CreateKingdomClan();
        var courtingClanId = CreateKingdomClan();
        var retiringId = AddMember(closingClanId);
        var formerPartnerId = AddMember(partnerClanId);
        var courtingId = AddMember(courtingClanId);
        var courtedId = AddMember(courtingClanId);

        environment.Server.Call(() =>
        {
            var retiring = environment.Server.GetRegisteredObject<Hero>(retiringId);
            environment.Server.GetRegisteredObject<Clan>(closingClanId).SetLeader(retiring);
            environment.Server.GetRegisteredObject<Clan>(partnerClanId)
                .SetLeader(environment.Server.GetRegisteredObject<Hero>(formerPartnerId));
            environment.Server.GetRegisteredObject<Clan>(courtingClanId)
                .SetLeader(environment.Server.GetRegisteredObject<Hero>(courtingId));
            Romance.SetRomanticState(
                retiring,
                environment.Server.GetRegisteredObject<Hero>(formerPartnerId),
                Romance.RomanceLevelEnum.FailedInCompatibility);
            Romance.SetRomanticState(
                environment.Server.GetRegisteredObject<Hero>(courtingId),
                environment.Server.GetRegisteredObject<Hero>(courtedId),
                Romance.RomanceLevelEnum.CourtshipStarted);

            environment.Server.Resolve<IApplyHeirSelectionActionInterface>().ApplyByRetirementWithoutHeir(retiring);
        }, TextMethods);
        environment.FlushCoalescer();

        var listener = new CommentOnChangeRomanticStateBehavior();
        client.Call(() =>
        {
            Assert.Null(client.GetRegisteredObject<Hero>(retiringId).Clan);
            listener.RegisterEvents();
        });

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestRomanceStateSync()));
        environment.FlushCoalescer();
        environment.Server.PumpGameThread();
        client.PumpGameThread();

        client.Call(() =>
        {
            CampaignEvents.RomanticStateChanged.ClearListeners(listener);
            var retired = client.GetRegisteredObject<Hero>(retiringId);
            var courting = client.GetRegisteredObject<Hero>(courtingId);
            var logs = Campaign.Current.LogEntryHistory.GetGameActionLogs<ChangeRomanticStateLogEntry>(_ => true);

            Assert.Equal(
                Romance.RomanceLevelEnum.FailedInCompatibility,
                Romance.GetRomanticLevel(retired, client.GetRegisteredObject<Hero>(formerPartnerId)));
            Assert.Equal(
                Romance.RomanceLevelEnum.CourtshipStarted,
                Romance.GetRomanticLevel(courting, client.GetRegisteredObject<Hero>(courtedId)));

            // Both pairs include a clan leader, so vanilla logs each one, including the clanless hero's.
            Assert.Single(logs, log => log.Hero1 == retired || log.Hero2 == retired);
            Assert.Single(logs, log => log.Hero1 == courting || log.Hero2 == courting);
        });
    }

    // Creates a clan inside a kingdom, which is what makes retiring without an heir close the clan.
    private string CreateKingdomClan()
    {
        string clanId = null;
        environment.Server.Call(() =>
        {
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var clan = Clan.CreateClan("RomanceSnapshotClan");
            clan._defaultPartyTemplate = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
            clan._kingdom = kingdom;
            kingdom._clans.Add(clan);
            Assert.True(environment.Server.ObjectManager.TryGetId(clan, out clanId));
        });
        return clanId;
    }

    // Adds an active adult hero to the clan.
    private string AddMember(string clanId)
    {
        var heroId = environment.CreateRegisteredObject<Hero>();
        environment.Server.Call(() =>
        {
            var hero = environment.Server.GetRegisteredObject<Hero>(heroId);
            hero._heroState = Hero.CharacterStates.Active;
            hero.SetBirthDay(CampaignTime.YearsFromNow(-30));
            hero.Clan = environment.Server.GetRegisteredObject<Clan>(clanId);
        });
        return heroId;
    }
}
