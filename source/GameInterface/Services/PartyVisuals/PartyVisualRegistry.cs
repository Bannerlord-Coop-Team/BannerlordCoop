using GameInterface.Registry;
using GameInterface.Services.PartyBases.Extensions;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals
{
    internal class PartyVisualRegistry : RegistryBase<MobilePartyVisual>
    {
        private const string PartyVisualIdPrefix = $"Coop{nameof(MobilePartyVisual)}";
        private int InstanceCounter = 0;

        public PartyVisualRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            var visualManager = MobilePartyVisualManager.Current;

            if (visualManager == null)
            {
                Logger.Error("Unable to register party visuals when PartyVisualManager is null");
                return;
            }

            foreach (var party in MobileParty.All)
            {
                var mobilePartyVisual = party.Party.GetPartyVisual();

                if (mobilePartyVisual == null) continue;

                var networkId = $"{nameof(mobilePartyVisual)}_{party.StringId}";
                RegisterExistingObject(networkId, mobilePartyVisual);
            }

            foreach (MobilePartyVisual visual in visualManager._visualsFlattened)
            {
                RegisterNewObject(visual, out var _);
            }
        }

        protected override string GetNewId(MobilePartyVisual visual)
        {
            return $"{PartyVisualIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}
