using GameInterface.AutoSync;
using GameInterface.AutoSync.Registry;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CultureObjects;
internal class CultureObjectSync : IAutoSync
{
    public CultureObjectSync(IAutoRegistryFactory registryFactory)
    {
        // Lifetime
        var cultureObjectCtors = AccessTools.GetDeclaredConstructors(typeof(CultureObject));
        registryFactory.TryRegisterType<CultureObject>(cultureObjectCtors, RegisterAll, OnCultureObjectCreated);

        var basicCultureObjectCtors = AccessTools.GetDeclaredConstructors(typeof(BasicCultureObject));
        registryFactory.TryRegisterType<BasicCultureObject>(basicCultureObjectCtors, RegisterAll);
    }

    private void OnCultureObjectCreated(string id, CultureObject cultureObject)
    {
        cultureObject.StringId = id;
        MBObjectManager.Instance.RegisterPresumedObject(cultureObject);
    }

    private void RegisterAll(AutoRegistry<BasicCultureObject> registry)
    {
        foreach (var culture in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
        {
            registry.RegisterNewObject(culture, out _);
        }
    }

    private void RegisterAll(AutoRegistry<CultureObject> registry)
    {
        foreach (var culture in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
        {
            registry.RegisterNewObject(culture, out _);
        }
    }
}
