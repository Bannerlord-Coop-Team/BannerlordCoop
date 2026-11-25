using ProtoBuf;

namespace GameInterface.Services.AdvancedStart.Data;

/// <summary>The advanced starting options the server lets a joining player pick from.</summary>
[ProtoContract(SkipConstructor = true)]
public class AdvancedStartPolicy
{
    [ProtoMember(1)]
    public readonly string[] StartTypes;
    [ProtoMember(2)]
    public readonly string DefaultStartType;
    /// <summary>The campaign has been resumed, so start types that change the world are locked.</summary>
    [ProtoMember(3)]
    public readonly bool WorldStarted;
    /// <summary>Kingdoms whose resolved kingdom a player already rules, which a king start can't take.</summary>
    [ProtoMember(4)]
    public readonly string[] PlayerRuledKingdomIds;
    /// <summary>Kingdoms a kingdom start can join in this world, by the id the window lists them under. World
    /// scenarios eliminate some and merge others, such as the imperial kingdoms in a United Empire world.</summary>
    [ProtoMember(5)]
    public readonly string[] JoinableKingdomIds;
    /// <summary>Offered start types that change the world, locked once <see cref="WorldStarted"/>.</summary>
    [ProtoMember(6)]
    public readonly string[] WorldStartTypes;
    /// <summary>Offered start types that join the kingdom picked in the KingdomId option.</summary>
    [ProtoMember(7)]
    public readonly string[] KingdomStartTypes;
    /// <summary>Option ids of the on/off modifiers a joining player can tick on top of their start.</summary>
    [ProtoMember(8)]
    public readonly string[] ModifierIds;

    public AdvancedStartPolicy(string[] startTypes, string[] worldStartTypes, string[] kingdomStartTypes, string[] modifierIds,
        string defaultStartType, bool worldStarted, string[] playerRuledKingdomIds, string[] joinableKingdomIds)
    {
        StartTypes = startTypes;
        WorldStartTypes = worldStartTypes;
        KingdomStartTypes = kingdomStartTypes;
        ModifierIds = modifierIds;
        DefaultStartType = defaultStartType;
        WorldStarted = worldStarted;
        PlayerRuledKingdomIds = playerRuledKingdomIds;
        JoinableKingdomIds = joinableKingdomIds;
    }
}
