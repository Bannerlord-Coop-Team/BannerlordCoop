using Common.Logging;
using GameInterface.AutoSync;
using GameInterface.AutoSync.Registry;
using HarmonyLib;
using Serilog;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.BesiegerCamps
{
    internal class BesiegerCampSync : IAutoSync
    {
        readonly ILogger Logger = LogManager.GetLogger<BesiegerCampSync>();

        public BesiegerCampSync(IAutoSyncBuilder autoSyncBuilder, IAutoRegistryFactory registryFactory)
        {
            // Lifetime
            var ctors = AccessTools.GetDeclaredConstructors(typeof(BesiegerCamp));
            registryFactory.TryRegisterType<BesiegerCamp>(ctors, RegisterAll, OnClientRegister);

            // Fields
            autoSyncBuilder.AddField(AccessTools.Field(typeof(BesiegerCamp), nameof(BesiegerCamp._leaderParty)));

            // Props
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.NumberOfTroopsKilledOnSide)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeEvent)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeEngines)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeStrategy)));
        }

        void RegisterAll(AutoRegistry<BesiegerCamp> registry)
        {
            foreach (var camp in Campaign.Current.SiegeEventManager.SiegeEvents.Select(siegeEvent => siegeEvent.BesiegerCamp))
            {
                if (registry.RegisterNewObject(camp, out _) == false) Logger.Error($"Unable to register {camp}");
            }
        }

        void OnClientRegister(BesiegerCamp newInstance)
        {
            AccessTools.Field(typeof(BesiegerCamp), nameof(BesiegerCamp._besiegerParties)).SetValue(newInstance, new MBList<MobileParty>());
        }
    }
}