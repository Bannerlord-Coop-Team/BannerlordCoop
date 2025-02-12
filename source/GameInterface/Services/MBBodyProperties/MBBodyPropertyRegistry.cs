using GameInterface.Services.Registry;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.MBBodyProperties;
internal class MBBodyPropertyRegistry : RegistryBase<MBBodyProperty>
{
    private const string IdPrefix = "CoopMBBodyProperty";
    private int InstanceCounter = 0;

    public MBBodyPropertyRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        foreach (CharacterObject character in Campaign.Current.Characters)
        {
            if (TryGetId(character, out _)) continue;

            RegisterExistingObject(GetNewId(character.BodyPropertyRange), character);
        }
    }

    protected override string GetNewId(MBBodyProperty party)
    {
        return $"{IdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
