using System.Collections.Generic;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Quests.TheConquestOfSettlement;

internal sealed class TheConquestOfSettlementQuestOwnerSaveData
{
    [SaveableField(1)]
    internal string QuestId;

    [SaveableField(2)]
    internal string ControllerId;

    private TheConquestOfSettlementQuestOwnerSaveData()
    {
    }

    internal TheConquestOfSettlementQuestOwnerSaveData(string questId, string controllerId)
    {
        QuestId = questId;
        ControllerId = controllerId;
    }
}

public sealed class TheConquestOfSettlementSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 44_186_003;

    public TheConquestOfSettlementSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(TheConquestOfSettlementQuestOwnerSaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<TheConquestOfSettlementQuestOwnerSaveData>));
    }
}
