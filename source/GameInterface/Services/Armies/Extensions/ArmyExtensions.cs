using Common.Extensions;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using Serilog.Core;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Extensions
{
    internal static class ArmyExtensions
    {
        private static readonly ILogger Logger = LogManager.GetLogger<Army>();

        private static Action<Army, MobileParty> Army_OnAddPartyInternal = typeof(Army).GetMethod("OnAddPartyInternal", BindingFlags.NonPublic | BindingFlags.Instance)
            .BuildDelegate<Action<Army, MobileParty>>();
        private static Action<Army, MobileParty> Army_OnRemovePartyInternal = typeof(Army).GetMethod("OnRemovePartyInternal", BindingFlags.NonPublic | BindingFlags.Instance)
            .BuildDelegate<Action<Army, MobileParty>>();
        
        internal static void OnAddPartyInternal(this MobileParty mobileParty, Army army)
        {
            Army_OnAddPartyInternal(army,mobileParty);
        }
        internal static void OnRemovePartyInternal(this MobileParty mobileParty, Army army)
        {
            Army_OnRemovePartyInternal(army,mobileParty);
        }

        public static string GetStringId(this Army army)
        {
            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                Logger.Error("Unable to get {objectManager}", nameof(IObjectManager));
                return null;
            }

            if (objectManager.TryGetId(army, out var id) == false)
            {
                Logger.Error("{army} was not properly registered", army.Name);
            }

            return id;
        }
    }
}