using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Policies;

/// <summary>
/// Registry for <see cref="PolicyObject"/>. Registers the static, XML-defined policy objects so
/// they resolve by network id across machines (required by both the policy sync and the existing
/// KingdomPolicyDecisionData lookups).
/// </summary>
internal class PolicyObjectRegistry : AutoRegistryBase<PolicyObject>
{
    public PolicyObjectRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(PolicyObject), new Type[] { typeof(string) })
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (PolicyObject policy in PolicyObject.All)
        {
            RegisterExistingObject(policy.StringId, policy);
        }
    }

    public override void OnClientCreated(PolicyObject obj, string id)
    {
    }

    public override void OnClientDestroyed(PolicyObject obj, string id)
    {
    }

    public override void OnServerCreated(PolicyObject obj, string id)
    {
    }

    public override void OnServerDestroyed(PolicyObject obj, string id)
    {
    }
}
