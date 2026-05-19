using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.PartyTemplateObjects;

internal class PartyTemplateObjectRegistry : AutoRegistryBase<PartyTemplateObject>
{
    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();
    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public PartyTemplateObjectRegistry(
        ILogger logger,
        IAutoRegistryFactory autoRegistryFactory,
        IObjectManager objectManager) : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override void RegisterAllObjects()
    {
        foreach (var partyTemplateObject in MBObjectManager.Instance.GetObjectTypeList<PartyTemplateObject>())
        {
            objectManager.AddExisting(partyTemplateObject.StringId, partyTemplateObject);
        }
    }

    public override void OnClientCreated(PartyTemplateObject obj, string id)
    {
    }

    public override void OnClientDestroyed(PartyTemplateObject obj, string id)
    {
    }

    public override void OnServerCreated(PartyTemplateObject obj, string id)
    {
    }

    public override void OnServerDestroyed(PartyTemplateObject obj, string id)
    {
    }
}
