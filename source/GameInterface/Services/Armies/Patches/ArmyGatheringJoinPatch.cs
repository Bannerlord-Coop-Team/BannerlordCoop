using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Armies.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Patches;

/// <summary>
/// Routes the "answer the call to arms" army join, where a lord is still mustering, so the membership
/// reaches the server instead of existing only on the joining client.
/// </summary>
/// <remarks>
/// Vanilla's consequence is a bare <c>MobileParty.MainParty.Army = hero.PartyBelongedTo.Army</c> with no
/// <c>AddPartyToMergedParties</c> - this is the "join now, ride to the muster point yourself" join, and it is
/// the one vanilla path that legitimately produces an army member whose <c>AttachedTo</c> is null.
///
/// Unpatched it half-applies on a client: <c>MobileParty.set_Army</c> stores the backing field BEFORE calling
/// <c>Army.OnAddPartyInternal</c>, and <see cref="ArmyPatches"/>' prefix on that method returns false on a
/// client without publishing anything. The client then believes it is in an army that the server has no
/// record of - its army overlay and menus read a membership the authoritative world does not have, and a
/// later "leave army" publishes a removal for something never recorded.
///
/// Deliberately does NOT merge the party: vanilla does not attach here, and forcing an attach would change
/// the gameplay meaning of answering a call to arms. The resulting AttachedTo-less member is exactly the
/// state vanilla's MapEvent.Initialize skips during a siege, which is why the server seats besieging players
/// explicitly (ServerSiegeEntryHandler.JoinConnectedBesiegerAttackers).
/// </remarks>
[HarmonyPatch(typeof(LordConversationsCampaignBehavior))]
internal class ArmyGatheringJoinPatch
{
    [HarmonyPatch("conversation_lord_tell_gathering_player_joined_on_consequence")]
    [HarmonyPrefix]
    private static bool Prefix()
    {
        // The server has no MainParty, and its own join paths already replicate.
        if (ModInformation.IsServer) return true;

        var army = Hero.OneToOneConversationHero?.PartyBelongedTo?.Army;
        if (army == null) return true;

        var mainParty = MobileParty.MainParty;
        if (mainParty == null || mainParty.Army == army) return false;

        using (new AllowedThread())
        {
            ArmyPatches.AddMobilePartyInArmy(mainParty, army);
        }

        MessageBroker.Instance.Publish(mainParty,
            new MobilePartyInArmyAdded(army, mainParty, addPartyToMergedPartiesBool: false));

        return false;
    }
}
