using Common.Messaging;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.ObjectManager;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies;

public interface IArmyDisbander
{
    void Disband(Army army, Army.ArmyDispersionReason reason);
}

internal sealed class ArmyDisbander : IArmyDisbander
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public ArmyDisbander(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
    }

    public void Disband(Army army, Army.ArmyDispersionReason reason)
    {
        if (army._armyIsDispersing)
            return;

        var parties = army.Parties.ToArray();
        // Vanilla's own-army check, a co-op player's army would open a menu on the server that stops its AI
        CampaignEventDispatcher.Instance.OnArmyDispersed(
            army,
            reason,
            parties.Contains(MobileParty.MainParty));
        army._armyIsDispersing = true;

        try
        {
            foreach (var party in parties)
            {
                messageBroker.Publish(party, new MobilePartyInArmyRemoved(army, party, null));
                ArmyPatches.RemoveMobilePartyInArmyImmediate(party, army, null);
            }

            army._parties.Clear();
            army.Kingdom = null;
            army._hourlyTickEvent?.DeletePeriodicEvent();
            army._tickEvent?.DeletePeriodicEvent();
        }
        finally
        {
            army._armyIsDispersing = false;
        }

        if (objectManager.Contains(army))
            messageBroker.Publish(army, new InstanceDestroyed<Army>(army));
    }
}
