using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.PartyBases;
internal class PartyBaseSync : IAutoSync
{
    ILogger Logger { get; }

    public PartyBaseSync(IAutoSyncBuilder autoSyncBuilder, IAutoRegistryFactory registryFactory, ILogger logger)
    {
        // Lifetime Sync
        var ctors = new MethodBase[] {
                AccessTools.Constructor(typeof(PartyBase), new Type[] { typeof(MobileParty), typeof(Settlement) })
            };
        registryFactory.TryRegisterType<PartyBase>(ctors, RegisterAll);

        // Property Sync
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.MobileParty)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.Settlement)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.IsVisualDirty)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.ItemRoster)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.LevelMaskIsDirty)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.MemberRoster)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.PrisonRoster)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(PartyBase), nameof(PartyBase.RandomValue)));

        // Field Sync
        autoSyncBuilder.AddField(AccessTools.Field(typeof(PartyBase), nameof(PartyBase._customOwner)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(PartyBase), nameof(PartyBase._index)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(PartyBase), nameof(PartyBase._lastEatingTime)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(PartyBase), nameof(PartyBase._lastNumberOfMenPerTierVersionNo)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(PartyBase), nameof(PartyBase._lastNumberOfMenWithHorseVersionNo)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(PartyBase), nameof(PartyBase._mapEventSide)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(PartyBase), nameof(PartyBase._numberOfMenWithHorse)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(PartyBase), nameof(PartyBase._remainingFoodPercentage)));
    }

    private void RegisterAll(AutoRegistry<PartyBase> registry)
    {
        foreach (var party in MobileParty.All)
        {
            if (registry.RegisterNewObject(party.Party, out var _) == false) Logger.Error("Unable to register PartyBase from Party with the object manager");
        }
    }
}
