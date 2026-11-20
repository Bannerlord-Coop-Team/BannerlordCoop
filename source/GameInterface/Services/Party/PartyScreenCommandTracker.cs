using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Party;

/// <summary>
/// Records the party screen commands that actually changed the open screen, in the order they ran.
/// </summary>
internal interface IPartyScreenCommandTracker : IGameAbstraction
{
    IReadOnlyList<PartyScreenLogic.PartyCommand> GetCommands(PartyScreenLogic logic);
    /// <summary>How many commands ran before the last <see cref="Save"/>.</summary>
    int GetSavedCount(PartyScreenLogic logic);
    void Record(PartyScreenLogic logic, PartyScreenLogic.PartyCommand command);
    void Clear(PartyScreenLogic logic);
    void Save(PartyScreenLogic logic);
    void RestoreSaved(PartyScreenLogic logic);
}

internal class PartyScreenCommandTracker : IPartyScreenCommandTracker
{
    private readonly List<PartyScreenLogic.PartyCommand> commands = new();
    private PartyScreenLogic trackedLogic;
    private int savedCount;

    public IReadOnlyList<PartyScreenLogic.PartyCommand> GetCommands(PartyScreenLogic logic)
        => ReferenceEquals(logic, trackedLogic)
            ? commands.ToArray()
            : Array.Empty<PartyScreenLogic.PartyCommand>();

    public int GetSavedCount(PartyScreenLogic logic)
        => ReferenceEquals(logic, trackedLogic) ? savedCount : 0;

    public void Record(PartyScreenLogic logic, PartyScreenLogic.PartyCommand command)
    {
        // A new screen gets a new logic, so the first record on it drops the last screen's commands.
        if (!ReferenceEquals(logic, trackedLogic)) Track(logic);
        commands.Add(command);
    }

    public void Clear(PartyScreenLogic logic)
    {
        if (!ReferenceEquals(logic, trackedLogic)) return;
        commands.Clear();
        savedCount = 0;
    }

    public void Save(PartyScreenLogic logic)
    {
        if (!ReferenceEquals(logic, trackedLogic)) Track(logic);
        savedCount = commands.Count;
    }

    public void RestoreSaved(PartyScreenLogic logic)
    {
        if (!ReferenceEquals(logic, trackedLogic)) return;
        if (savedCount < commands.Count) commands.RemoveRange(savedCount, commands.Count - savedCount);
    }

    private void Track(PartyScreenLogic logic)
    {
        trackedLogic = logic;
        commands.Clear();
        savedCount = 0;
    }
}
