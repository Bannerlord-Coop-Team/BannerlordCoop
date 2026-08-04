using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// This quest's core structural difference from every prior Group A type, and this file's whole reason to exist
/// (see <see cref="ILandLordCompanyOfTroubleIssueInterface"/>'s type doc comment for the full derivation): for
/// most of the quest's life the "mercs" are just troops embedded directly in the OWNER'S OWN
/// <c>MobileParty.MainParty.MemberRoster</c> - not a separate <see cref="MobileParty"/> at all. Only when the
/// hostile-turn dialogue branch ("So be it!") fires does the private <c>CreateCompanyEnemyParty()</c> spin up a
/// real <c>BanditPartyComponent.CreateBanditParty(...)</c> party - and BOTH its spawn position
/// (<c>MobileParty.MainParty.Position</c>) AND its troop count (<c>_companyTroopCount</c>) are read from the
/// OWNER'S OWN live state AT THAT EXACT TRIGGER MOMENT, not at accept time. <c>BanditPartyComponent</c>'s ctor is
/// hard-blocked on a client
/// (<see cref="GameInterface.Services.PartyComponents.BanditPartyComponents.Lifetime.BanditPartyComponentLifetimePatches"/>),
/// the same "client-authority gap" shape every other Group A type's party construction had.
///
/// <c>CreateCompanyEnemyParty()</c> is Category B (design doc §5) - independently re-verified rather than
/// trusted: it's only ever reached via <c>ConversationEndOneShot</c>, itself only ever subscribed from the
/// "So be it!" option inside <c>GetCompanyDialogFlow()</c>, itself only ever opened by
/// <c>company_of_trouble_menu_on_init</c>'s <c>_triggerCompanyOfTroubleConversation</c> branch, itself only ever
/// set true by this quest's own <c>HourlyTick()</c> - whose own gating condition
/// (<c>MobileParty.MainParty.MemberRoster.TotalManCount - _companyTroopCount &lt;= _companyTroopCount</c>) is
/// self-limiting to the genuine owner (a non-owner's own mirror always has <c>_companyTroopCount == 0</c>, since
/// their own MainParty never received the embedded-troop injection either - see
/// <see cref="LandLordCompanyOfTroubleOwnershipGatePatches"/>'s own doc comment for the one place this
/// "no non-owner path" reasoning does NOT hold). So this Prefix needs no ownership check of its own - only a
/// client/host branch, exactly like <c>CaravanAmbushPartySpawnGatePatch</c>'s own shape:
///
/// - Host-owner: real vanilla body runs COMPLETELY UNMODIFIED (its own <c>MobileParty.MainParty</c> reads are
///   already correct, since the host genuinely IS the accepter) - zero divergence, single-player-equivalent
///   behavior. A Postfix then captures+broadcasts whatever the real body produced.
/// - Client-owner: <c>BanditPartyComponent</c>'s ctor would be blocked, so the Prefix instead (1) refreshes+
///   captures <c>_companyTroopCount</c>/<c>MobileParty.MainParty.Position</c> at this exact moment (see
///   <see cref="ILandLordCompanyOfTroubleIssueInterface.RefreshCompanyTroopCount"/>'s doc comment for why this is
///   a faithful reimplementation, not a reflective invoke of vanilla's own <c>UpdateCompanyTroopCount</c>);
///   (2) runs the one real-body line that's 100% safe locally regardless of client/host - removing the embedded
///   troops from the OWNER'S OWN already-existing roster, not a blocked construction; (3) forwards the captured
///   position+count to the server instead of constructing the party locally.
///
/// TASK ITEM 4 fold-in: see <see cref="ILandLordCompanyOfTroubleIssueInterface"/>'s type doc comment for why this
/// same round trip also serves as this quest's "battle-start-approval" - <c>_battleWillStart</c> only ever
/// becomes true (on ANY machine, including the true owner's own) once
/// <see cref="Handlers.LandLordCompanyOfTroubleIssueHandler"/>'s broadcast lands.
/// </summary>
[HarmonyPatch(typeof(LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest))]
internal class LandLordCompanyOfTroubleEnemyPartySpawnGatePatch
{
    [HarmonyPatch("CreateCompanyEnemyParty")]
    [HarmonyPrefix]
    private static bool Prefix(LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest __instance)
    {
        if (!ModInformation.IsClient) return true; // host-owner: real vanilla body runs unmodified

        var owner = __instance.QuestGiver;
        if (owner == null) return false;

        ContainerProvider.TryResolve<ILandLordCompanyOfTroubleIssueInterface>(out var issueInterface);
        if (issueInterface == null) return false;

        // Refresh+capture at THIS exact trigger moment, on the genuine accepter's own machine - the core
        // "trigger-time, not creation-time" capture this quest type needs (see the type doc comment).
        var troopCount = issueInterface.RefreshCompanyTroopCount(owner);
        var position = MobileParty.MainParty?.Position ?? default;

        // The one real-body line that's 100% safe to run locally regardless of client/host - mutates the
        // OWNER'S OWN already-existing MainParty roster, not a blocked PartyComponent construction. Matches
        // vanilla's own first line exactly.
        if (MobileParty.MainParty != null)
        {
            MobileParty.MainParty.MemberRoster.AddToCounts(__instance._troubleCharacterObject, -troopCount);
        }

        MessageBroker.Instance.Publish(__instance, new LandLordCompanyOfTroubleEnemyPartySpawnRequested(owner, position, troopCount));

        return false;
    }

    [HarmonyPatch("CreateCompanyEnemyParty")]
    [HarmonyPostfix]
    private static void Postfix(LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest __instance)
    {
        if (ModInformation.IsClient) return;

        // Genuine host-owner's own real CreateCompanyEnemyParty() just ran, vanilla unmodified - broadcast so
        // every OTHER peer's mirror force-writes _companyOfTroubleParty (and flips _battleWillStart) to
        // reference this same, already-created state. The client-forwarded path
        // (Handlers.LandLordCompanyOfTroubleIssueHandler.Handle_NetworkEnemyPartySpawnRequest) never reaches
        // this method at all - it calls ILandLordCompanyOfTroubleIssueInterface.CreateCompanyEnemyPartyOnServer
        // directly and publishes the same LandLordCompanyOfTroubleEnemyPartySpawned event itself.
        ContainerProvider.TryResolve<ILandLordCompanyOfTroubleIssueInterface>(out var issueInterface);
        if (issueInterface == null) return;

        var owner = __instance.QuestGiver;
        if (!issueInterface.TryCaptureCompanyEnemyParty(owner, out var party)) return;

        MessageBroker.Instance.Publish(__instance, new LandLordCompanyOfTroubleEnemyPartySpawned(owner, party));
    }
}
