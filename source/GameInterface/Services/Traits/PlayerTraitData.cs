using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Traits;

/// <summary>
/// Campaign.PlayerTraitDeveloper holds trait xp for only one player
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class PlayerTraitData
{
    // Dictionary<PlayerHeroId, Dictionary<TraitId, TraitXp>>
    [ProtoMember(1)]
    public Dictionary<string, Dictionary<string, int>> PlayerTraitXp { get; }

    public PlayerTraitData(Dictionary<string, Dictionary<string, int>> playerTraitXp)
    {
        PlayerTraitXp = playerTraitXp ?? new();
    }
}
