using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies;

public interface IArmyLeaver
{
    /// <summary>
    /// Takes this client's own party out of its army, if it is in one. Client side.
    /// </summary>
    void Leave(MobileParty party);
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

    public void Leave(MobileParty party)
    {
        var army = party.Army;
        if (army == null) return;

        messageBroker.Publish(army, new MobilePartyInArmyRemoved(army, party, clientMobileParty: party));
        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                ArmyPatches.RemoveMobilePartyInArmyImmediate(party, army, clientMobileParty: party);
            }
        });
    }
}
