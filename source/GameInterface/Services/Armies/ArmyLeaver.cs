using Common.Messaging;
using Common.Util;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies;

public interface IArmyLeaver
{
    /// <summary>
    /// Takes this client's own party out of its army. Client side.
    /// </summary>
    void Leave(MobileParty party, Army army);
}

/// <summary>
/// A client's own army leave: asks the server for it and applies it locally ahead of the server's echo,
/// because <see cref="ArmyPatches"/> drops a client's native <see cref="MobileParty.Army"/> write without publishing it.
/// </summary>
internal sealed class ArmyLeaver : IArmyLeaver
{
    private readonly IMessageBroker messageBroker;

    public ArmyLeaver(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
    }

    public void Leave(MobileParty party, Army army)
    {
        messageBroker.Publish(army, new MobilePartyInArmyRemoved(army, party, party));
        using (new AllowedThread())
        {
            ArmyPatches.RemoveMobilePartyInArmy(party, army, party);
        }
    }
}
