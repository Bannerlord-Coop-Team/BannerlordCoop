using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface;
using GameInterface.Services.GameState.Messages;
using HarmonyLib;
using Missions.Missiles.Patches;
using Serilog;
using System;

namespace Missions.Missiles;

/// <summary>
/// Installs this assembly's categorized projectile-sync patches once the campaign is ready. The client patches
/// only GameInterface by default, so patches in Missions require explicit installation.
/// </summary>
public class MissilePatchInstaller : IHandler
{
    public const string MissilePatchCategory = "CoopMissilePatches";

    private static readonly ILogger Logger = LogManager.GetLogger<MissilePatchInstaller>();
    private static readonly Harmony Harmony = new Harmony(GameInterfaceModule.HarmonyId);

    // Patches persist for the process (UnpatchAll is disabled), so a later load must not re-run PatchCategory
    // or the postfix would attach twice and each shot would send two missiles.
    private static bool applied;

    private readonly IMessageBroker messageBroker;

    public MissilePatchInstaller(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<CampaignReady>(Handle_CampaignReady);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignReady>(Handle_CampaignReady);
    }

    private void Handle_CampaignReady(MessagePayload<CampaignReady> payload)
    {
        if (applied) return;

        try
        {
            Harmony.PatchCategory(typeof(AddMissileAuxPatch).Assembly, MissilePatchCategory);
            // Latch only after success so a failed apply retries on the next CampaignReady.
            applied = true;
            Logger.Information("Applied coop missile sync patches ({Category}) on campaign ready", MissilePatchCategory);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply coop missile sync patches");
        }
    }
}
