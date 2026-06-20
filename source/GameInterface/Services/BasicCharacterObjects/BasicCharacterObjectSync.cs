using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCharacterObjects
{
    internal class BasicCharacterObjectSync : IAutoSync
    {
        public BasicCharacterObjectSync(AutoSyncRegistry AutoSyncRegistry)
        {
            // Fields
            AutoSyncRegistry.AddField(AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject._isBasicHero)));
            AutoSyncRegistry.AddField(AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject._isMounted)));
            AutoSyncRegistry.AddField(AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject._isRanged)));
            AutoSyncRegistry.AddField(AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject._equipmentRoster)));
            AutoSyncRegistry.AddField(AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject.DefaultCharacterSkills)));
            AutoSyncRegistry.AddField(AccessTools.Field(typeof(BasicCharacterObject), nameof(BasicCharacterObject._basicName)));

            // Properties
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.Age)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.Culture)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.DefaultFormationClass)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.DefaultFormationGroup)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.DismountResistance)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.BodyPropertyRange)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.FaceDirtAmount)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.FaceMeshCache)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.FormationPositionPreference)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.IsFemale)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.IsObsolete)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.IsSoldier)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.KnockbackResistance)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.KnockdownResistance)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.Level)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCharacterObject), nameof(BasicCharacterObject.Race)));

            // Targetmethods
        }
    }
}
