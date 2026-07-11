using GameInterface.AutoSync;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyBases;
internal class PartyBaseSync : IAutoSync
{
    ILogger Logger { get; }

    public PartyBaseSync(AutoSyncRegistry autoSyncBuilder, ILogger logger)
    {
        // Property Sync
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.MobileParty)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.Settlement)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.ItemRoster)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.MemberRoster)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.PrisonRoster)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.RandomValue)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.CustomName)));

        // Cached values, does not need sync
        //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.IsVisualDirty)));
        //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.LevelMaskIsDirty)));

        // Field Sync
        autoSyncBuilder.AddField(AccessTools.Field(typeof(PartyBase), nameof(PartyBase._customOwner)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(PartyBase), nameof(PartyBase._index)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(PartyBase), nameof(PartyBase._lastEatingTime)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(PartyBase), nameof(PartyBase._remainingFoodPercentage)));
        // MapEvent initialization and side membership own both ends of this relationship atomically.

        // Cached values, does not need sync
        //autoSyncBuilder.AddField(AccessTools.Field(typeof(PartyBase), nameof(PartyBase._numberOfMenWithHorse)));
        //autoSyncBuilder.AddField(AccessTools.Field(typeof(PartyBase), nameof(PartyBase._lastNumberOfMenPerTierVersionNo)));
        //autoSyncBuilder.AddField(AccessTools.Field(typeof(PartyBase), nameof(PartyBase._lastNumberOfMenWithHorseVersionNo)));
    }
}
