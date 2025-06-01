using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCharacterObjects
{
    internal class BasicCharacterObjectSync : IDynamicSync
    {
        public BasicCharacterObjectSync(DynamicSyncRegistry dynamicSyncRegistry)
        {
            // Fields
            dynamicSyncRegistry.AddField(AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject._isBasicHero)));
            dynamicSyncRegistry.AddField(AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject._isMounted)));
            dynamicSyncRegistry.AddField(AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject._isRanged)));
            dynamicSyncRegistry.AddField(AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject._equipmentRoster)));
            dynamicSyncRegistry.AddField(AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject.DefaultCharacterSkills)));
            dynamicSyncRegistry.AddField(AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject._basicName)));

            // Properties
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.Age)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.BeardTags)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.Culture)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.DefaultFormationClass)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.DefaultFormationGroup)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.DismountResistance)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.BodyPropertyRange)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.FaceDirtAmount)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.FaceMeshCache)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.FormationPositionPreference)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.HairTags)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.IsFemale)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.IsObsolete)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.IsSoldier)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.KnockbackResistance)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.KnockdownResistance)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.Level)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.Race)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.TattooTags)));

            // Targetmethods
        }
    }
}
