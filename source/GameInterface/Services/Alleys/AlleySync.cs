using Common.Logging;
using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Alleys;

public class AlleySync : IAutoSync
{
    ILogger Logger { get; } = LogManager.GetLogger<AlleySync>();
    static int callCount = 0;
    public AlleySync(IAutoSyncBuilder autoSyncBuilder) 
    {
        // Fields
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._name)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._settlement)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._tag)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._owner)));
    }
}
