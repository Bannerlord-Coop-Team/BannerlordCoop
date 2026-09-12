using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkDoRefinement : ICommand
{
    [ProtoMember(1)]
    public readonly string CraftingHeroId;

    [ProtoMember(2)]
    public readonly CraftingMaterials Input1;

    [ProtoMember(3)]
    public readonly int Input1Count;

    [ProtoMember(4)]
    public readonly CraftingMaterials Input2;

    [ProtoMember(5)]
    public readonly int Input2Count;

    [ProtoMember(6)]
    public readonly CraftingMaterials Output1;

    [ProtoMember(7)]
    public readonly int Output1Count;

    [ProtoMember(8)]
    public readonly CraftingMaterials Output2;

    [ProtoMember(9)]
    public readonly int Output2Count;

    public NetworkDoRefinement(
        string craftingHeroId,
        CraftingMaterials input1,
        int input1Count,
        CraftingMaterials input2,
        int input2Count,
        CraftingMaterials output1,
        int output1Count,
        CraftingMaterials output2,
        int output2Count)
    {
        CraftingHeroId = craftingHeroId;
        Input1 = input1;
        Input1Count = input1Count;
        Input2 = input2;
        Input2Count = input2Count;
        Output1 = output1;
        Output1Count = output1Count;
        Output2 = output2;
        Output2Count = output2Count;
    }
}