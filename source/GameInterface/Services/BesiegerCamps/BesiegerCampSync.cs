using Common.Logging;
using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
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

        public BesiegerCampSync(IAutoSyncBuilder autoSyncBuilder)
        {
            // Fields
            autoSyncBuilder.AddField(AccessTools.Field(typeof(BesiegerCamp), nameof(BesiegerCamp._leaderParty)));

            // Props
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.NumberOfTroopsKilledOnSide)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeEvent)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeEngines)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BesiegerCamp), nameof(BesiegerCamp.SiegeStrategy)));
        }
    }
}