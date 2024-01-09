using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Utils;

[HarmonyPatch]
internal class MethodTimeoutHelper
{
    static IEnumerable<MethodBase> TargetMethods() => new MethodInfo[]
    {
        typeof(Campaign).GetMethod(nameof(Campaign.LateAITick)),
    };

    private static readonly ILogger Logger = LogManager.GetLogger<MethodTimeoutHelper>();
    
    private static Task TimeoutTask;
    private static CancellationTokenSource cts;

    static void Prefix()
    {
        cts = new CancellationTokenSource();
        TimeoutTask = Task.Delay(1000, cts.Token).ContinueWith(task => { Handle(Environment.StackTrace); }, TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    static void Postfix()
    {
        cts.Cancel();
    }

    static void Handle(string callStack)
    {
        Logger.Error($"Timeout Detected, dumping call stack, \n{callStack}");
    }
}
