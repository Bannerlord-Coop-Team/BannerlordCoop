using Common.Messaging;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies;

public interface IArmyPartyRemover
{
    /// <summary>
    /// Takes a party out of its army and replicates the removal to clients. Server side.
    /// </summary>
    void Remove(MobileParty party, Army army);
}

/// <summary>
/// The server's own removal of a party from an army. <see cref="ArmyPatches.RemoveMobilePartyInArmy"/> edits the army
/// directly and never reaches the <see cref="Army.OnRemovePartyInternal"/> prefix that publishes, so this publishes first.
/// </summary>
internal sealed class ArmyPartyRemover : IArmyPartyRemover
{
    private readonly IMessageBroker messageBroker;

    public ArmyPartyRemover(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
    }

    public void Remove(MobileParty party, Army army)
    {
        messageBroker.Publish(party, new MobilePartyInArmyRemoved(army, party, null));
        ArmyPatches.RemoveMobilePartyInArmy(party, army, null);
    }
}
