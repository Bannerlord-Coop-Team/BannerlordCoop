using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.CharacterObjects;
internal class CharacterObjectSync : IAutoSync
{
    public CharacterObjectSync(IAutoRegistryFactory autoRegistryFactory)
    {
        var ctors = new MethodBase[] { AccessTools.Constructor(typeof(CharacterObject)) };
        autoRegistryFactory.TryRegisterType<CharacterObject>(ctors, RegisterAllCharacterObjects);

        var basicCtors = new MethodBase[] { AccessTools.Constructor(typeof(BasicCharacterObject)) };
        autoRegistryFactory.TryRegisterType<BasicCharacterObject>(basicCtors, RegisterAllBasicCharacterObjects);
    }

    private void RegisterAllCharacterObjects(AutoRegistry<CharacterObject> registry)
    {
        foreach (CharacterObject character in CharacterObject.All)
        {
            registry.RegisterNewObject(character, out _);
        }
    }

    private void RegisterAllBasicCharacterObjects(AutoRegistry<BasicCharacterObject> registry)
    {
        foreach (CharacterObject character in CharacterObject.All)
        {
            registry.RegisterNewObject(character, out _);
        }
    }
}
