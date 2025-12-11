using ProtoBuf;
using GameInterface.Services.ItemRosters.Data;
using System;
using System.Linq;

namespace GameInterface.Services.Heroes.Data;

[ProtoContract(SkipConstructor = true)]
public class GameObjectGuids
{
    [ProtoMember(1)]
    public string[] ControlledHeros { get; set; } = Array.Empty<string>();

    [ProtoMember(2)]
    public ItemRosterOwner[] ItemRosterOwners { get; set; } = Array.Empty<ItemRosterOwner>();


    public GameObjectGuids(string[] controlledHeros)
    {
        ControlledHeros = controlledHeros;
    }

    public override bool Equals(object obj)
    {
        if (obj is GameObjectGuids otherObjectGuids == false) return false;

        if (ControlledHeros != null && otherObjectGuids.ControlledHeros != null)
        {
            if (ControlledHeros.SequenceEqual(otherObjectGuids.ControlledHeros) == false) return false;
        }
        else
        {
            if (ControlledHeros != otherObjectGuids.ControlledHeros) return false;
        }

        return true;
    }

    public override int GetHashCode() => base.GetHashCode();
}
