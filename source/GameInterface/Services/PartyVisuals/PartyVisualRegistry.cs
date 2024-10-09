using GameInterface.Services.Registry;
using SandBox.View.Map;
using System.Threading;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.PartyVisuals
{
    internal class PartyVisualRegistry : RegistryBase<PartyVisual>
    {
        private const string PartyVisualIdPrefix = $"Coop{nameof(PartyVisual)}";
        private static int InstanceCounter = 0;

        public PartyVisualRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            var visualManager = PartyVisualManager.Current;

            if (visualManager == null)
            {
                Logger.Error("Unable to register party visuals when PartyVisualManager is null");
                return;
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
