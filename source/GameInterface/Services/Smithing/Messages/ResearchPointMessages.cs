using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Messages;

public readonly struct UpdateResearchPoints : IEvent
{
    public readonly Hero MainHero;
    public readonly CraftingTemplate CraftingTemplate;
    public readonly float NewXp;

    public UpdateResearchPoints(Hero mainHero, CraftingTemplate craftingTemplate, float newXp)
    {
        MainHero = mainHero;
        CraftingTemplate = craftingTemplate;
        NewXp = newXp;
    }
}

public readonly struct OpenCraftingPart : IEvent
{
    public readonly Hero MainHero;
    public readonly CraftingTemplate CraftingTemplate;
    public readonly CraftingPiece CraftingPiece;

    public OpenCraftingPart(Hero mainHero, CraftingTemplate craftingTemplate, CraftingPiece craftingPiece)
    {
        MainHero = mainHero;
        CraftingTemplate = craftingTemplate;
        CraftingPiece = craftingPiece;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateResearchPoints : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly string CraftingTemplateId;

    [ProtoMember(3)]
    public readonly float NewXp;

    public NetworkUpdateResearchPoints(string playerHeroId, string craftingTemplateId, float newXp)
    {
        PlayerHeroId = playerHeroId;
        NewXp = newXp;
        CraftingTemplateId = craftingTemplateId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkOpenCraftingPart : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly string CraftingTemplateId;

    [ProtoMember(3)]
    public readonly string CraftingPieceId;

    public NetworkOpenCraftingPart(string playerHeroId, string craftingTemplateId, string craftingPieceId)
    {
        PlayerHeroId = playerHeroId;
        CraftingTemplateId = craftingTemplateId;
        CraftingPieceId = craftingPieceId;
    }
}