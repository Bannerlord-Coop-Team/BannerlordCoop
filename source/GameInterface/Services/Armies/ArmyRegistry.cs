using Common;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using SandBox.View.Map;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Armies;

/// <summary>
/// Registry for <see cref="Army"/> type
/// </summary>
internal class ArmyRegistry : AutoRegistryBase<Army>
{
    public ArmyRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(Army));

    public override IEnumerable<MethodBase> DestroyMethods => new MethodBase[]
    {
        AccessTools.Method(typeof(Army), nameof(Army.DisperseInternal))
    };

    public override void RegisterAllObjects()
    {
        IEnumerable<Kingdom> kingdoms = Campaign.Current?.Kingdoms ?? Enumerable.Empty<Kingdom>();

        foreach (var kingdom in kingdoms)
        {
            foreach (var army in kingdom.Armies)
            {
                RegisterExistingObject(kingdom.StringId, army);
            }
        }
    }

    public override void OnClientCreated(Army obj, string id)
    {
        AccessTools.Field(typeof(Army), nameof(Army._parties)).SetValue(obj, new MBList<MobileParty>());

        // The client Army is created via SkipConstructor, so the periodic tick events
        // (_hourlyTickEvent / _tickEvent) are never initialized. Native methods such as
        // DisperseInternal dereference them, so initialize them the same way the
        // constructor does by invoking the private AddEventHandlers.
        obj.AddEventHandlers();
    }
    // DisperseInternal doesnt work since  it accesses LeaderParty.Position, tick events, and
    // CampaignEventDispatcher which arent initialized on client objects (SkipConstructor).
    // Just clean up the fields directly.
    public override void OnClientDestroyed(Army obj, string id)
    {
        GameThread.Run(() =>
        {
            bool containsMainParty = false;
            using (new AllowedThread())
            {
                foreach (var party in obj._parties)
                {
                    if (party == MobileParty.MainParty)
                    {
                        containsMainParty = true;
                    }
                    party.AttachedTo = null;
                    party._army = null;
                }
                obj._parties.Clear();
                // Explicitly null MainParty in case it wasn't in _parties
                if (MobileParty.MainParty._army == obj)
                {
                    MobileParty.MainParty._army = null;
                }
                obj.Kingdom = null; //remove army from kingdom

                obj._hourlyTickEvent?.DeletePeriodicEvent();
                obj._tickEvent?.DeletePeriodicEvent();
                // this is what  removes the overlay panel
                var mapScreen = Game.Current.GameStateManager.ActiveState as MapState;
                // overlay needs to be removed only when the mainparty is in the army
                if (mapScreen != null && (containsMainParty || obj.LeaderParty == MobileParty.MainParty))
                {
                    var screen = ScreenManager.TopScreen as MapScreen;
                    screen?.RemoveArmyOverlay();
                }
            }
        });
    }

    public override void OnServerCreated(Army obj, string id)
    {
    }

    public override void OnServerDestroyed(Army obj, string id)
    {
    }
}
