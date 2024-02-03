using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem;
using Common.Logging;
using Serilog;
using Common.Extensions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Extensions
{
    internal static class DisbandArmyActionExtension
    {
        private static readonly ILogger Logger = LogManager.GetLogger<Army>();

        private static Action<Army, Army.ArmyDispersionReason> DisbandArmyAction_ApplyInternal = typeof(DisbandArmyAction).GetMethod("ApplyInternal", BindingFlags.NonPublic | BindingFlags.Static)
            .BuildDelegate<Action<Army, Army.ArmyDispersionReason>>();

        internal static void ApplyInternal(this Army army, Army.ArmyDispersionReason reason)
        {
            DisbandArmyAction_ApplyInternal(army, reason);
        }
    }






}
