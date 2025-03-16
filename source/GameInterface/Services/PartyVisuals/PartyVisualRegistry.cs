using GameInterface.Registry;
using GameInterface.Services.PartyBases.Extensions;
using SandBox.View.Map;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.PartyVisuals
{
    internal class PartyVisualRegistry : RegistryBase<PartyVisual>
    {
        private const string PartyVisualIdPrefix = $"Coop{nameof(PartyVisual)}";
        private int InstanceCounter = 0;

        public PartyVisualRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            var visualManager = PartyVisualManager.Current;

            if (visualManager == null)
            {
                Logger.Error("Unable to register party visuals when PartyVisualManager is null");
                return;
            }

            foreach (var party in MobileParty.All)
            {
                var partyVisual = party.Party.GetPartyVisual();

                if (partyVisual == null) continue;

                var networkId = $"{nameof(PartyVisual)}_{party.StringId}";
                RegisterExistingObject(networkId, partyVisual);
            }

            foreach (PartyVisual visual in visualManager._visualsFlattened)
            {
                RegisterNewObject(visual, out var _);
            }
        }

        protected override string GetNewId(PartyVisual visual)
        {
            return $"{PartyVisualIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}
