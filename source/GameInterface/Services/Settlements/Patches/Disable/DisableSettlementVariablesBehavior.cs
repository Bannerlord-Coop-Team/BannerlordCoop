using Common.Logging;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Settlements.Patches.Disable;


[HarmonyPatch(typeof(SettlementVariablesBehavior))]
internal class DisableSettlementVariablesBehavior
{
    private static readonly ILogger Logger = LogManager.GetLogger<DisableSettlementVariablesBehavior>();

    [HarmonyPatch(nameof(SettlementVariablesBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (ContainerProvider.TryResolve<IGameInterfaceConfig>(out var config) == false)
        {
            Logger.Error("Unable to resolve {type}\n"
                    + "Callstack: {callstack}", typeof(IGameInterfaceConfig), Environment.StackTrace);
            return true;
        }

        return config.IsServer;
    }
}
