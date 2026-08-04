using Common;
using Common.Logging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using SandBox.Issues;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Finding B's real redesign (doc/RivalGangMovingIn_Design_v2.md §6) - the genuinely new problem is synchronous
/// ordering inside <c>StartAlleyBattle()</c>, not routing. <c>StartAlleyBattle()</c> structurally only ever runs
/// on the genuine owner's own process (Category B, §6.1), so it never needs to be "replayed on the server" - the
/// prior (superseded) design's premise. Both of its mission-launch calls,
/// <c>PlayerEncounter.RestartPlayerEncounter(...)</c> and <c>PlayerEncounter.StartBattle()</c>, already route
/// correctly through this project's EXISTING, unmodified client-request/server-authoritative pipeline
/// (<c>PlayerEncounterPatches.RestartPlayerEncounterPrefix</c> -&gt; <c>ConversationRequestHandler</c> -&gt;
/// <c>PlayerEncounterPatches.StartBattleInternalPrefix</c> -&gt; <c>MapEventCreationCoordinator.RequestBlocking</c>)
/// - no new network protocol needed for either call in isolation.
///
/// The real gap: <c>RestartPlayerEncounterPrefix</c>'s client branch is fire-and-forget (publishes
/// <c>ConversationRequested</c> and returns immediately - the real body that sets <c>PlayerEncounter.Current</c>
/// only runs later, asynchronously, once server approval lands and <c>Handle_NetworkAllowConversation</c>
/// re-runs it under <c>AllowedThread</c>). But vanilla's <c>StartAlleyBattle()</c> calls
/// <c>PlayerEncounter.StartBattle()</c> on the very next line - <c>StartBattle()</c>'s own body is
/// <c>return Current.StartBattleInternal();</c>, a direct, UNGUARDED dereference of <c>Current</c>. On a remote-
/// client owner, <c>Current</c> is still null at that point (the approval round trip hasn't landed yet), so this
/// NREs immediately - a real, blocking correctness gap specific to this quest's synchronous scripted-battle-
/// trigger shape (every other <c>RestartPlayerEncounter</c> caller in this codebase is tick/event-driven, not
/// chained synchronously into an immediate <c>StartBattle()</c> the way this quest is).
///
/// Fix: on a client only, reimplement <c>StartAlleyBattle()</c>'s sequence unchanged except for one insertion -
/// a blocking wait, using the exact same <c>GameThread.WaitWhilePumping</c>/<see cref="INetworkConfig.ObjectCreationTimeout"/>
/// primitive <c>MapEventCreationCoordinator.RequestBlocking</c> already uses - placed immediately after
/// <c>RestartPlayerEncounter(...)</c> and before <c>StartBattle()</c>. The wait condition
/// (<c>PlayerEncounter.Current != null &amp;&amp; PlayerEncounter.EncounteredParty == _rivalGangLeaderParty.Party</c>)
/// mirrors the exact duplicate-approval check <c>ConversationRequestHandler.Handle_NetworkAllowConversation</c>
/// already uses. On the host-owns-the-quest path, this whole reimplementation is skipped and vanilla's real body
/// runs synchronously and unmodified - <c>PlayerEncounter.Current</c> is set immediately there, so no wait is
/// needed, matching every other host-is-owner short-circuit in this family.
///
/// Also closes the party-spawn-gate's own graceful-abort case here (rather than leaving an un-mitigated NRE, as
/// the design doc accepts as a fallback): if <see cref="Patches.RivalGangMovingInPartySpawnGatePatch"/>'s
/// coordinator request timed out/was rejected, the 4 party/hero fields are still null after
/// <c>CreateRivalGangLeaderParty()</c>/<c>CreateAllyGangLeaderParty()</c> return - this reimplementation checks
/// for that explicitly and aborts with a log instead of dereferencing a null <c>_rivalGangLeaderParty</c>.
/// </summary>
[HarmonyPatch(typeof(RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest))]
internal class RivalGangMovingInAlleyBattlePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<RivalGangMovingInAlleyBattlePatch>();

    private static readonly Type QuestType = typeof(RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest);
    private static readonly MethodInfo CreateRivalGangLeaderPartyMethod = AccessTools.Method(QuestType, "CreateRivalGangLeaderParty");
    private static readonly MethodInfo CreateAllyGangLeaderPartyMethod = AccessTools.Method(QuestType, "CreateAllyGangLeaderParty");
    private static readonly MethodInfo PreparePlayerPartyMethod = AccessTools.Method(QuestType, "PreparePlayerParty");
    private static readonly FieldInfo IsReadyToBeFinalizedField = AccessTools.Field(QuestType, "_isReadyToBeFinalized");

    [HarmonyPatch("StartAlleyBattle")]
    [HarmonyPrefix]
    private static bool Prefix(RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest __instance)
    {
        if (!ModInformation.IsClient) return true; // host-owner: vanilla unmodified, PlayerEncounter.Current is set synchronously

        RunOnClient(__instance);
        return false;
    }

    private static void RunOnClient(RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest quest)
    {
        // Invoked via reflection so RivalGangMovingInPartySpawnGatePatch's own Harmony prefixes on these exact
        // methods still intercept them (Harmony patches the underlying method regardless of call site).
        CreateRivalGangLeaderPartyMethod.Invoke(quest, null);
        CreateAllyGangLeaderPartyMethod.Invoke(quest, null);

        if (!ContainerProvider.TryResolve<IRivalGangMovingInIssueInterface>(out var issueInterface) ||
            !issueInterface.TryCaptureRivalGangParties(quest.QuestGiver, out var rivalGangLeaderParty, out _,
                out var allyGangLeaderParty, out _))
        {
            Logger.Error(
                "Aborting the rival gang alley battle for {Owner}: the rival gang parties were not created " +
                "(see RivalGangMovingInPartySpawnGatePatch's log for why) - continuing would NRE identically to " +
                "the un-mitigated vanilla bug this patch replaces.",
                quest.QuestGiver?.StringId);
            return;
        }

        PreparePlayerPartyMethod.Invoke(quest, null);

        PlayerEncounter.RestartPlayerEncounter(rivalGangLeaderParty.Party, PartyBase.MainParty, false, false);

        if (!ContainerProvider.TryResolve<INetworkConfig>(out var configuration))
        {
            Logger.Error("Aborting the rival gang alley battle for {Owner}: no INetworkConfig available", quest.QuestGiver?.StringId);
            return;
        }

        var deadline = DateTime.UtcNow + configuration.ObjectCreationTimeout;
        if (!GameThread.WaitWhilePumping(
                () => PlayerEncounter.Current != null && ReferenceEquals(PlayerEncounter.EncounteredParty, rivalGangLeaderParty.Party),
                deadline))
        {
            Logger.Error(
                "Timed out waiting for the server-approved PlayerEncounter restart before the rival gang alley " +
                "battle for {Owner}; aborting gracefully instead of dereferencing a still-null PlayerEncounter.Current",
                quest.QuestGiver?.StringId);
            return;
        }

        PlayerEncounter.StartBattle();
        allyGangLeaderParty.MapEventSide = PlayerEncounter.Battle.GetMapEventSide(PlayerEncounter.Battle.PlayerSide);
        GameMenu.ActivateGameMenu("rival_gang_quest_after_fight");
        IsReadyToBeFinalizedField.SetValue(quest, true);

        if (issueInterface.TryCaptureRivalGangParties(quest.QuestGiver, out _, out var rivalGangLeaderHenchmanHero, out _, out _))
        {
            PlayerEncounter.StartCombatMissionWithDialogueInTownCenter(rivalGangLeaderHenchmanHero.CharacterObject);
        }
    }
}
