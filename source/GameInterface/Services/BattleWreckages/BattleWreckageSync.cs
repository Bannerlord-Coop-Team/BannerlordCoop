using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.BattleWreckages;

namespace GameInterface.Services.BattleWreckages;

internal class BattleWreckageSync : IAutoSync
{
    public BattleWreckageSync(AutoSyncRegistry AutoSyncRegistry)
    {
        //// Fields
        // Keep these uncoalesced (the default), so position and type reach clients before NetworkAddBattleWreckage builds the map visual
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.Position)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.DestroyTime)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.WreckageTypeCategory)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage._isInvestigated)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.AttackerLeaderPartyName)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.DefenderLeaderPartyName)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.AttackerFaction)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.DefenderFaction)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.WinnerSide)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.BattleStartTime)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.AttackerHealthyTroopCountAtStart)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.DefenderHealthyTroopCountAtStart)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.AttackerDiedInBattle)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.DefenderDiedInBattle)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.AttackerWoundedInBattle)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.DefenderWoundedInBattle)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.AttackerLeaderHero)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(BattleWreckage), nameof(BattleWreckage.DefenderLeaderHero)));

        //// Properties
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BattleWreckage), nameof(BattleWreckage.Name)));
    }
}
