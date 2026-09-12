using Common;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEngines.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines.Handlers;

/// <summary>
/// Reconstructs a replicated siege bombardment missile on the client and adds it to the firing side, so the
/// map view renders the projectile. The client never runs BombardTick, so it also prunes deprecated missiles
/// on each add to keep the list from growing.
/// </summary>
internal class SiegeEngineMissileHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public SiegeEngineMissileHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<ApplySiegeEngineMissile>(HandleApply);
    }

    private void HandleApply(MessagePayload<ApplySiegeEngineMissile> payload)
    {
        var obj = payload.What;
        // Resolve on the game thread so the siege event and target engine lookups stay ordered behind their registrations.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<SiegeEvent>(obj.SiegeEventId, out var siegeEvent)) return;

            // Catalog SiegeEngineTypes are XML objects the co-op registry never holds; resolve through the game's manager.
            var shooterType = MBObjectManager.Instance.GetObject<SiegeEngineType>(obj.ShooterEngineTypeId);
            if (shooterType == null) return;

            SiegeEngineConstructionProgress targetEngine = null;
            if (!string.IsNullOrEmpty(obj.TargetSiegeEngineId))
                objectManager.TryGetObject(obj.TargetSiegeEngineId, out targetEngine);

            var side = siegeEvent.GetSiegeEventSide((BattleSideEnum)obj.Side);

            // Point the firing engine at its target so the map view aims and animates its model. Pin Previous to Current:
            // outside the animation window the view faces the previous target and inside it the current one, and the
            // client campaign clock only advances in coarse correction steps, so a previous-to-current turn stutters
            // across that window.
            var deployedRanged = side.SiegeEngines.DeployedRangedSiegeEngines;
            if (obj.ShooterSlotIndex >= 0 && obj.ShooterSlotIndex < deployedRanged.Length)
            {
                var ranged = deployedRanged[obj.ShooterSlotIndex]?.RangedSiegeEngine;
                if (ranged != null)
                {
                    ranged.CurrentTargetType = (SiegeBombardTargets)obj.TargetType;
                    ranged.CurrentTargetIndex = obj.TargetSlotIndex;
                    ranged.PreviousDamagedTargetType = (SiegeBombardTargets)obj.TargetType;
                    ranged.PreviousTargetIndex = obj.TargetSlotIndex;
                    // The view animates the reload/fire arm cycle approaching NextProjectileCollisionTime (the engine's
                    // next fire time, a reload interval ahead), slinging only in its final slice. Fed the server's
                    // absolute value, the client's trailing clock stays short of that slice and each new missile resets
                    // it, so the arm sticks cocked. Anchor to the client clock, keeping the server's fire-to-next-fire
                    // span, so the client sweeps through the fire phase before the next missile arrives.
                    var now = CampaignTime.Now;
                    long reloadTicks = obj.CollisionTicks - obj.FireTicks;
                    if (reloadTicks < 0) reloadTicks = 0;
                    ranged.LastBombardTime = now;
                    ranged.NextTimeEngineCanBombard = now + new CampaignTime(reloadTicks);
                }
            }

            var missile = new SiegeEngineMissile(shooterType, obj.ShooterSlotIndex, (SiegeBombardTargets)obj.TargetType,
                obj.TargetSlotIndex, targetEngine, new CampaignTime(obj.CollisionTicks), new CampaignTime(obj.FireTicks), obj.HitSuccessful);

            side.AddSiegeEngineMissile(missile);
            side.RemoveDeprecatedMissiles();
        });
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ApplySiegeEngineMissile>(HandleApply);
    }
}
