using Common.Messaging;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using System.Linq;
using TaleWorlds.CampaignSystem;

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
        CampaignEventDispatcher.Instance.OnArmyDispersed(
            army,
            reason,
            parties.Any(party => party.IsPlayerParty()));
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
