using Common;
using Common.Util;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

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
    }

    public override void OnClientDestroyed(Army obj, string id)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                foreach (var party in obj._parties)
                {
                    party.AttachedTo = null;
                    party._army = null;
                }
                obj._parties.Clear();
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
