using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Messages;

public record ResearchPointsUpdated : IEvent
{
    public Hero MainHero;
    public CraftingTemplate CraftingTemplate;
    public float NewXp;

    public ResearchPointsUpdated(Hero mainHero, CraftingTemplate craftingTemplate, float newXp)
    {
        MainHero = mainHero;
        CraftingTemplate = craftingTemplate;
        NewXp = newXp;
    }
}

public record CraftingPartOpened : IEvent
{
    public Hero MainHero;
    public CraftingTemplate CraftingTemplate;
    public CraftingPiece CraftingPiece;

    public CraftingPartOpened(Hero mainHero, CraftingTemplate craftingTemplate, CraftingPiece craftingPiece)
    {
        MainHero = mainHero;
        CraftingTemplate = craftingTemplate;
        CraftingPiece = craftingPiece;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkUpdateResearchPoints : ICommand
{
    [ProtoMember(1)]
    public string PlayerHeroId;

    [ProtoMember(2)]
    public string CraftingTemplateId;

    [ProtoMember(3)]
    public float NewXp;

    public NetworkUpdateResearchPoints(string playerHeroId, string craftingTemplateId, float newXp)
    {
        PlayerHeroId = playerHeroId;
        NewXp = newXp;
        CraftingTemplateId = craftingTemplateId;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkOpenCraftingPart : ICommand
{
    [ProtoMember(1)]
    public string PlayerHeroId;

    [ProtoMember(2)]
    public string CraftingTemplateId;

    [ProtoMember(3)]
    public string CraftingPieceId;

    public NetworkOpenCraftingPart(string playerHeroId, string craftingTemplateId, string craftingPieceId)
    {
        PlayerHeroId = playerHeroId;
        CraftingTemplateId = craftingTemplateId;
        CraftingPieceId = craftingPieceId;
    }
}