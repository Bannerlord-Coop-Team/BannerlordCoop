using Common;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using SandBox.GauntletUI;
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
        obj.Cohesion = 100f;
        obj._armyGatheringStartTime = 0f;
        obj._creationTime = CampaignTime.Now;
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
            using (new AllowedThread())
            {
                if (obj._armyIsDispersing)
                {
                    return;
                }
                CampaignEventDispatcher.Instance.OnArmyDispersed(obj, Army.ArmyDispersionReason.Unknown, obj.Parties.Contains(MobileParty.MainParty));
                obj._armyIsDispersing = true;
                foreach (var party in obj._parties)
                {
                    if (MobileParty.MainParty != null)
                    {
                        party.Party.UpdateVisibilityAndInspected(MobileParty.MainParty.Position, 0f);
                    }
                    if (MobileParty.MainParty != party)
                    {
                        party.Ai.RethinkAtNextHourlyTick = true;
                    }
                    party.AttachedTo = null;
                    party._army = null;
                }
                obj._parties.Clear();
                obj.Kingdom = null;
                if (obj.LeaderParty == MobileParty.MainParty)
                {
                    MapState mapState = Game.Current.GameStateManager.ActiveState as MapState;
                    if (mapState != null)
                    {
                        mapState.OnDispersePlayerLeadedArmy();
                    }
                }
                obj._hourlyTickEvent?.DeletePeriodicEvent();
                obj._tickEvent?.DeletePeriodicEvent();
                obj._armyIsDispersing = false;
                // KingdomArmyVm.RefreshArmyList in DisbandCurrentArmy() is too early, call it when the army is destroyed
                if (ScreenManager.TopScreen is GauntletKingdomScreen kingdomScreen)
                {
                    kingdomScreen.DataSource?.Army?.RefreshArmyList();
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
