﻿using GameInterface.Services.Registry;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Armies;

/// <summary>
/// Registry for <see cref="CharacterObject"/> type
/// </summary>
internal class CharacterObjectRegistry : RegistryBase<CharacterObject>
{
    private const string CharacterObjectPrefix = "CoopCharacterObject";
    private static int InstanceCounter = 0;

    public CharacterObjectRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        foreach(CharacterObject character in Campaign.Current.Characters)
        {
            if (RegisterNewObject(character, out var _) == false)
            {
                Logger.Error($"Unable to register {character}");
            }
        }
    }

    protected override string GetNewId(CharacterObject obj)
    {
        return $"{CharacterObjectPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
