using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Tournaments;

public interface ITournamentSaveDeferral : IGameAbstraction
{
    bool TryDefer(SaveHandler.SaveArgs.SaveMode saveType, string saveName);
    void Flush();
}

public sealed class TournamentSaveDeferral : ITournamentSaveDeferral
{
    private readonly object gate = new();
    private readonly ITournamentSessionRegistry sessionRegistry;
    private readonly Queue<DeferredSave> deferredSaves = new();

    public TournamentSaveDeferral(ITournamentSessionRegistry sessionRegistry)
    {
        this.sessionRegistry = sessionRegistry;
    }

    public bool TryDefer(SaveHandler.SaveArgs.SaveMode saveType, string saveName)
    {
        lock (gate)
        {
            if (!sessionRegistry.HasActiveSessions)
                return false;

            deferredSaves.Enqueue(new DeferredSave(saveType, saveName));
            return true;
        }
    }

    public void Flush()
    {
        lock (gate)
        {
            if (sessionRegistry.HasActiveSessions || Campaign.Current?.SaveHandler == null)
                return;

            while (deferredSaves.Count > 0)
            {
                DeferredSave save = deferredSaves.Dequeue();
                Campaign.Current.SaveHandler.SetSaveArgs(save.SaveType, save.SaveName);
            }
        }
    }

    private readonly struct DeferredSave
    {
        public readonly SaveHandler.SaveArgs.SaveMode SaveType;
        public readonly string SaveName;

        public DeferredSave(SaveHandler.SaveArgs.SaveMode saveType, string saveName)
        {
            SaveType = saveType;
            SaveName = saveName;
        }
    }
}
