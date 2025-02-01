using GameInterface.Services.Registry;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobilePartyAIs;
internal class MobilePartyAiRegistry : RegistryBase<MobilePartyAi>
{
    private const string MobilePartyAiIdPrefix = "CoopMobilePartyAi";
    private static int InstanceCounter = 0;

    public MobilePartyAiRegistry(IRegistryCollection collection) : base(collection)
    {
    }

    public override void RegisterAll()
    {
        var objectManager = Campaign.Current?.CampaignObjectManager;

        if (objectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        foreach (var party in objectManager.MobileParties)
        {
            var partyAi = party.Ai;

            if (partyAi == null)
            {
                Logger.Warning("{partyName}'s Ai was null when registering", party.Name);
                continue;
            }

            var newId = GetNewId(partyAi);

            base.RegisterExistingObject(newId, partyAi);
        }
    }

    protected override string GetNewId(MobilePartyAi obj)
    {
        return $"{MobilePartyAiIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
