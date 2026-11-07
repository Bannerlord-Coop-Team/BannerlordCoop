using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.HeirSelection.Interfaces;
using GameInterface.Services.Heroes.Messages.RomanceFlow;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.CommentBehaviors;
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

    // A retired hero whose clan closed has no Clan, which makes the game's romance listener throw.
    [Fact]
    public void Snapshot_WithRetiredClanlessHero_KeepsLaterStatesOnClient()
    {
        var client = environment.Clients.First();
        var closingClanId = CreateKingdomClan();
        var otherClanId = CreateKingdomClan();
        var retiringId = AddMember(closingClanId);
        var formerPartnerId = AddMember(otherClanId);
        var courtingId = AddMember(otherClanId);
        var courtedId = AddMember(otherClanId);

        environment.Server.Call(() =>
        {
            var retiring = environment.Server.GetRegisteredObject<Hero>(retiringId);
            environment.Server.GetRegisteredObject<Clan>(closingClanId).SetLeader(retiring);
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
            Assert.Equal(
                Romance.RomanceLevelEnum.FailedInCompatibility,
                Romance.GetRomanticLevel(
                    client.GetRegisteredObject<Hero>(retiringId),
                    client.GetRegisteredObject<Hero>(formerPartnerId)));
            Assert.Equal(
                Romance.RomanceLevelEnum.CourtshipStarted,
                Romance.GetRomanticLevel(
                    client.GetRegisteredObject<Hero>(courtingId),
                    client.GetRegisteredObject<Hero>(courtedId)));
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
