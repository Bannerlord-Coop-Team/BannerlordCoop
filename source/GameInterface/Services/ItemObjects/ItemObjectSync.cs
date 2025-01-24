using System;
using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemObjects
{
    public class ItemObjectSync : IAutoSync
    {
        /**
        * HolsterPositionShift (Vec3) does not like to be serialized, not sure how to approach this one
        */
        public ItemObjectSync(IAutoSyncBuilder autoSyncBuilder)
        {
            //Fields
            autoSyncBuilder.AddField(AccessTools.Field(typeof(ItemObject), nameof(ItemObject.Type)));

            //Properties
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Weight)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Name)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.MultiMeshName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.HolsterMeshName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.HolsterWithWeaponMeshName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.HolsterPositionShift)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.FlyingMeshName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.BodyName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.HolsterBodyName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.CollisionBodyName)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.RecalculateBody)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Culture)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Difficulty)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.ScaleFactor)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.ItemFlags)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Appearance)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.WeaponDesign)));

            //Collection
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(ItemObject), nameof(ItemObject.ItemHolsters)));
        }
    }
}
