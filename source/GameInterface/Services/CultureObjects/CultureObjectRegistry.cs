using Common;
using GameInterface.Registry.Auto;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CultureObjects;
internal class CultureObjectRegistry : IAutoRegistry<CultureObject>
{
    public Type ObjectType => typeof(CultureObject);
    public IEnumerable<MethodBase> Constructors => throw new NotImplementedException();

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void OnClientCreated(CultureObject obj, string id)
    {
        obj.StringId = id;
        MBObjectManager.Instance.RegisterPresumedObject(obj);
    }

    public void OnClientDestroyed(CultureObject obj, string id)
    {
    }

    public void RegisterAllObjects(IRegistry<CultureObject> registry)
    {
        throw new NotImplementedException();
    }
}
