using System;
using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemObjects
{
    public class ItemObjectSync : IDynamicSync
    {
        public ItemObjectSync(DynamicSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(ItemObject), nameof(ItemObject.Type)));

            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.ItemComponent)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.MultiMeshName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.HolsterMeshName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.HolsterWithWeaponMeshName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.ItemHolsters)));
            //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.HolsterPositionShift))); // Prevents game from launching
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.HasLowerHolsterPriority)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.FlyingMeshName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.BodyName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.SkeletonName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.StaticAnimationName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.HolsterBodyName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.CollisionBodyName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.RecalculateBody)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.PrefabName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Name)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.ItemFlags)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.ItemCategory)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Value)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Effectiveness)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Weight)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Difficulty)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Appearance)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.IsUsingTableau)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.ArmBandMeshName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.IsFood)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.IsUniqueItem)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.ScaleFactor)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Culture)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.NotMerchandise)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.IsCraftedByPlayer)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.LodAtlasIndex)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.TierfOverride)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.WeaponDesign)));
        }
    }
}
