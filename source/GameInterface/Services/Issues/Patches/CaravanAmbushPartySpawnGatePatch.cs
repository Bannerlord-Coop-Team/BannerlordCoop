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
/// Real, independently-verified bug (the exact shape flagged going into this task - confirmed directly against
/// decompiled source rather than trusted blindly, for BOTH parties this quest spawns, not just one):
/// <c>CaravanAmbushIssueQuest.OnQuestAccepted()</c> - a live <c>OfferDialogFlow.Consequence</c>, only ever
/// reached on the genuine accepter's own machine (Category B in
/// doc/GroupA_HostileMobilePartySync_Design_v3.md §5) - calls BOTH
/// <c>CaravanPartyComponent.CreateCaravanParty(...)</c> and <c>BanditPartyComponent.CreateBanditParty(...)</c>
/// inline, in the same method. Both components' ctors are HARD-BLOCKED on a client
/// (<c>CaravanPartyComponentLifetimePatches.Prefix</c>/<c>BanditPartyComponentLifetimePatches.Prefix</c> both
/// return false there - confirmed by direct read, same shape as
/// <c>CustomPartyComponentLifetimePatches</c> for Smugglers), while the resulting <c>MobileParty</c>s
/// themselves are NOT blocked, just silently orphaned by
/// <c>GameInterface.Registry.Auto.LifetimePatches&lt;MobileParty&gt;.CreatePrefix</c> (logs an error on a
/// client, doesn't stop the constructor). Net effect on a client-accepter: two broken, split-brain "ghost"
/// parties only that one client can ever see, while the quest is permanently stuck for everyone else.
///
/// Unlike Smugglers, this quest has NO separable private "CreateXParty()" method to gate individually - both
/// party constructions, their roster fills, AI wiring, and the reward-item roll are all inline in
/// <c>OnQuestAccepted()</c> itself, alongside <c>StartQuest()</c> and the activation journal log. So this gate
/// covers the WHOLE method: on a client, <see cref="ICaravanAmbushIssueInterface.RunLocalAcceptSideEffects"/>
/// replicates the two parts that must stay local (<c>StartQuest()</c>/the journal log/the vicinity-check grace
/// timer - see that method's own doc comment for why these three don't need capture/force), then the party-
/// construction/roster/reward-roll portion is forwarded to the server instead of running locally - server-
/// authoritative, block-and-forward-as-request on a client, allow on server. See
/// <see cref="ICaravanAmbushIssueInterface"/>'s type doc comment for the full mechanism, including why
/// <c>accepterMainPartySpeed</c> (derived from <c>MobileParty.MainParty</c> inside the real method) is captured
/// HERE, on the accepter's own machine, before forwarding.
/// </summary>
[HarmonyPatch(typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest))]
internal class CaravanAmbushPartySpawnGatePatch
{
    [HarmonyPatch("OnQuestAccepted")]
    [HarmonyPrefix]
    private static bool Prefix(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest __instance)
    {
        if (!ModInformation.IsClient) return true;

        // Client: the real body would build two broken PartyComponents plus two orphaned, never-synced
        // MobileParty ghosts (see the type doc comment). Run only the safe, must-stay-local parts here, then
        // skip the rest.
        var owner = __instance.QuestGiver;
        if (owner == null) return false;

        ContainerProvider.TryResolve<ICaravanAmbushIssueInterface>(out var issueInterface);
        issueInterface?.RunLocalAcceptSideEffects(owner);

        var mainParty = MobileParty.MainParty;
        if (mainParty == null) return false;

        // Same value the (now-skipped) real method body would have read - captured from THIS machine's own
        // genuinely correct MainParty before forwarding, since the server's own MainParty would be the wrong
        // party's speed whenever this accepter is a remote client.
        var accepterMainPartySpeed = mainParty.Speed;

        MessageBroker.Instance.Publish(__instance, new CaravanAmbushAcceptRequested(owner, accepterMainPartySpeed));

        return false;
    }

    [HarmonyPatch("OnQuestAccepted")]
    [HarmonyPostfix]
    private static void Postfix(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest __instance)
    {
        if (ModInformation.IsClient) return;

        // Genuine server-side creation (the host's own live accept, unmodified) - broadcast so every OTHER
        // peer's mirror force-writes _caravanParty/_banditParty/_rewardItems to reference this same,
        // already-created state. The client-forwarded path (Handlers.CaravanAmbushIssueHandler.Handle_RequestCaravanAmbushAccept)
        // never reaches this method at all - it calls ICaravanAmbushIssueInterface.CreateReplicatedAccept
        // directly and publishes the same CaravanAmbushAccepted event itself.
        ContainerProvider.TryResolve<ICaravanAmbushIssueInterface>(out var issueInterface);
        if (issueInterface == null) return;

        var owner = __instance.QuestGiver;
        if (!issueInterface.TryCaptureAcceptedState(owner, out var caravanParty, out var banditParty, out var rewardItems)) return;

        MessageBroker.Instance.Publish(__instance, new CaravanAmbushAccepted(owner, caravanParty, banditParty, rewardItems));
    }
}
