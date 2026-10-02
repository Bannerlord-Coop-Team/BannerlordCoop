using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues;

internal sealed class SmugglersPlayerCharacterChangeScope : IDisposable
{
    [ThreadStatic]
    private static SmugglersPlayerCharacterChangeScope current;

    private readonly SmugglersPlayerCharacterChangeScope previous;
    private readonly Hero oldPlayer;

    internal static bool IsActive => current != null;

    internal SmugglersPlayerCharacterChangeScope(Hero oldPlayer)
    {
        this.oldPlayer = oldPlayer;
        previous = current;
        current = this;
    }

    internal static bool Affects(QuestBase quest, ISmugglersQuestOwners owners) => current != null
        && current.oldPlayer != null
        && owners.TryGet(quest, out var owner)
        && owner == current.oldPlayer;

    public void Dispose() => current = previous;
}
