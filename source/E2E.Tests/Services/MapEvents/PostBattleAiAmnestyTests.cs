using Common.Util;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// AI parties can't start an engagement with a player who is still in the post battle screens or in the
/// amnesty after them. The battle ends through the server's real finalize and the player leaves the screens
/// through a real client <see cref="PlayerEncounter.Finish"/>, so both signals run end to end.
/// </summary>
public class PostBattleAiAmnestyTests : MapEventTestBase
{
    public PostBattleAiAmnestyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void AiPartyCannotEngagePlayerStillInPostBattleScreens()
    {
        var (battleId, playerPartyId, aiPartyId) = SetupPlayerWithNearbyAiParty();
        Assert.True(CanEngage(aiPartyId, playerPartyId));

        DestroyServerMapEvent(battleId);
        // The screens aren't limited by the amnesty length.
        RunServerClock(seconds: 60f, paused: false);

        Assert.False(CanEngage(aiPartyId, playerPartyId));
    }

    [Fact]
    public void AmnestyRunsOutAfterLeavingScreens_OnlyWhileUnpaused()
    {
        var (battleId, playerPartyId, aiPartyId) = SetupPlayerWithNearbyAiParty();
        DestroyServerMapEvent(battleId);
        LeavePostBattleScreens(Clients.First(), playerPartyId);

        RunServerClock(seconds: 29f, paused: false);
        Assert.False(CanEngage(aiPartyId, playerPartyId));

        RunServerClock(seconds: 600f, paused: true);
        Assert.False(CanEngage(aiPartyId, playerPartyId));

        RunServerClock(seconds: 2f, paused: false);
        Assert.True(CanEngage(aiPartyId, playerPartyId));
    }

    [Fact]
    public void OtherPlayerCanStillEngageDuringAmnesty()
    {
        var (battleId, playerPartyId, aiPartyId) = SetupPlayerWithNearbyAiParty();
        var (_, otherPlayerPartyId) = CreatePlayerHeroParty("2");
        TestEnvironment.ConnectRegisteredPlayer(Clients.Last(), "2");
        DestroyServerMapEvent(battleId);
        LeavePostBattleScreens(Clients.First(), playerPartyId);

        Assert.False(CanEngage(aiPartyId, playerPartyId));
        Assert.True(CanEngage(otherPlayerPartyId, playerPartyId));
    }

    /// <summary>Player "1" on the first client attacks an AI party; a second AI party stands next to the player.</summary>
    private (string battleId, string playerPartyId, string aiPartyId) SetupPlayerWithNearbyAiParty()
    {
        var ctx = CreateServerMapEvent();
        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), ctx.AttackerPartyId);
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "1");

        return (ctx.MapEventId, ctx.AttackerPartyId, CreateServerParty());
    }

    private void LeavePostBattleScreens(EnvironmentInstance client, string partyId)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;
        });
        SetMockPlayerEncounter(client);
        EnableHeadlessEncounterFinish(client);

        client.Call(() => PlayerEncounter.Finish(), MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .ToList());

        AssertHasPlayerEncounter(client, expected: false);
    }

    // The campaign's per frame dt is zero while paused, whatever real time passes.
    private void RunServerClock(float seconds, bool paused)
    {
        Server.Call(() =>
        {
            Campaign.Current._dt = paused ? 0f : 0.25f * seconds;
            Server.Resolve<PostBattleAiAmnesty>().Tick(seconds);
        });
    }

    private bool CanEngage(string attackerPartyId, string targetPartyId)
    {
        var canEngage = false;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(attackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(targetPartyId, out var target));
            EnsureEncounterModel();
            // The attacker is heading for the target, the way the map AI sets it before an interaction.
            using (new AllowedThread())
            {
                attacker.ShortTermBehavior = AiBehavior.EngageParty;
                attacker.Ai.AiBehaviorInteractable = target.Party;
            }
            canEngage = ((IInteractablePoint)target.Party).CanPartyInteract(attacker, 0f);
        });
        return canEngage;
    }

    // The harness campaign has no encounter model, and the interaction distance check reads it.
    private static void EnsureEncounterModel()
    {
        if (Campaign.Current.Models.EncounterModel != null) return;

        var models = Campaign.Current.Models.GetGameModels().ToList();
        models.Add(new DefaultEncounterModel());
        Campaign.Current._gameModels = new GameModels(models);
    }
}
