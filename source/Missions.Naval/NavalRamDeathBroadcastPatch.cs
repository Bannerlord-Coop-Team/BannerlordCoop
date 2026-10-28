#if DEBUG
using Common.Messaging;
using HarmonyLib;
using Missions.Battles;
using Missions.Messages;
using Missions.Services.Network;

namespace Missions.Naval;

/// <summary>DEBUG: records death ids after the owner's battle-network broadcast returns.</summary>
[HarmonyPatch(typeof(LiteNetP2PClient), nameof(LiteNetP2PClient.SendAll), new[] { typeof(IMessage) })]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalRamDeathBroadcastPatch
{
    /// <summary>Only naval missions contribute; this does not change or resend the death message.</summary>
    [HarmonyPostfix]
    internal static void Postfix(IMessage message)
    {
        if (!CoopNavalMissionScope.IsActive || message is not NetworkBattleAgentDied death) return;
        NavalContactProbe.RecordDeathBroadcast(death.AgentId);
    }
}

/// <summary>DEBUG: records broadcasts received by the rammer so spared ids can be matched on that same client.</summary>
[HarmonyPatch(typeof(PuppetDeathApplier), "Handle_NetworkBattleAgentDied")]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalRamDeathReceivePatch
{
    /// <summary>Reads only the message id on the poll thread; no registry or campaign state is accessed.</summary>
    [HarmonyPostfix]
    internal static void Postfix(MessagePayload<NetworkBattleAgentDied> payload)
    {
        if (!CoopNavalMissionScope.IsActive) return;
        NavalContactProbe.RecordReceivedDeath(payload.What.AgentId);
    }
}
#endif
