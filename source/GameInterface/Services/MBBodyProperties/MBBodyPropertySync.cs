using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.MBBodyProperties;
internal class MBBodyPropertySync : IAutoSync
{
    public MBBodyPropertySync(IAutoRegistryFactory autoRegistryFactory)
    {
        var ctors = new MethodBase[] { AccessTools.Constructor(typeof(MBBodyProperty)) };
        autoRegistryFactory.TryRegisterType<MBBodyProperty>(ctors, RegisterAll);
    }

    private void RegisterAll(AutoRegistry<MBBodyProperty> registry)
    {
        foreach (CharacterObject character in Campaign.Current.Characters)
        {
            registry.RegisterNewObject(character.BodyPropertyRange, out _);
        }
    }
}
