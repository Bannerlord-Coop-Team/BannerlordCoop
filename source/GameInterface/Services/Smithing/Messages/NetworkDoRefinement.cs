using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkDoRefinement : ICommand
{
    [ProtoMember(1)]
    public string CraftingCampaignBehaviorId;

    [ProtoMember(2)]
    public string CraftingHeroId;

    [ProtoMember(3)]
    public CraftingMaterials Input1;

    [ProtoMember(4)]
    public int Input1Count;

    [ProtoMember(5)]
    public CraftingMaterials Input2;

    [ProtoMember(6)]
    public int Input2Count;

    [ProtoMember(7)]
    public CraftingMaterials Output1;

    [ProtoMember(8)]
    public int Output1Count;

    [ProtoMember(9)]
    public CraftingMaterials Output2;

    [ProtoMember(10)]
    public int Output2Count;

    public NetworkDoRefinement(
        string craftingCampaignBehaviorId,
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
        CraftingCampaignBehaviorId = craftingCampaignBehaviorId;
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