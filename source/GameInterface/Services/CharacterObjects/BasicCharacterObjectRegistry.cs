using GameInterface.Services.Registry;
using System;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Armies;

/// <summary>
/// Registry for <see cref="CharacterObject"/> type
/// </summary>
internal class BasicCharacterObjectRegistry : RegistryBase<BasicCharacterObject>
{
    public override IEnumerable<Type> ManagedTypes => new Type[]
    {
        typeof(BasicCharacterObject),
        typeof(CharacterObject)
    };

    private const string CharacterObjectPrefix = "CoopCharacterObject";
    private int InstanceCounter = 0;

    public BasicCharacterObjectRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        foreach (CharacterObject character in Campaign.Current.Characters)
        {
            if (TryGetId(character, out _)) continue;

            RegisterExistingObject(character.StringId, character);
        }
    }

    protected override string GetNewId(BasicCharacterObject obj)
    {
        return $"{CharacterObjectPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
