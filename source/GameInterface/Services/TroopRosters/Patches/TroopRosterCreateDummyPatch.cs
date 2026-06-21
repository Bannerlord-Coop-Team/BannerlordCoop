using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Patches;

[HarmonyPatch(typeof(TroopRoster))]
internal class TroopRosterCreateDummyPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterCreateDummyPatch>();

    private static readonly ConstructorInfo TroopRosterCtor = AccessTools.Constructor(typeof(TroopRoster), Type.EmptyTypes);

    [HarmonyPatch(nameof(TroopRoster.CreateDummyTroopRoster))]
    [HarmonyPrefix]
    private static bool Prefix_CreateDummyTroopRoster(ref TroopRoster __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
            return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed object {Type}", typeof(TroopRoster));
            return true;
        }

        __result = ObjectHelper.SkipConstructor<TroopRoster>();

        MessageBroker.Instance.Publish(null, new InstanceCreated<TroopRoster>(__result));

        TroopRosterCtor.Invoke(__result, Array.Empty<object>());

        return false;
    }
}
