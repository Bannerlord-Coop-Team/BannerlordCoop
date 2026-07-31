using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Save.Patches
{
    /// <summary>
    ///     Blocks the native save scheduler. <c>SetSaveArgs</c> is the private funnel behind
    ///     <c>SignalAutoSave</c>, <c>ForceAutoSave</c>, <c>SaveAs</c> and <c>QuickSaveCurrentGame</c>,
    ///     so suppressing it stops the whole native autosave timer.
    ///     <para>
    ///     Clients never save — the server is authoritative.
    ///     </para>
    ///     <para>
    ///     A <b>headless</b> host must not either, even though it is the server: the dedicated server
    ///     drives its own save schedule (<c>CoopServerHost.SaveNow</c> calls <c>Game.Current.Save</c>
    ///     directly), and running both is what froze campaign time in
    ///     <see href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/2540">issue #2540</see>.
    ///     <c>Game.SaveAux</c> parks a pending save's completion callback in the single-slot field
    ///     <c>Game._currentActiveSaveData</c>. When a second save starts while the first async write is
    ///     still in flight, that slot is overwritten and the native <c>SaveHandler.OnSaveCompleted</c>
    ///     — the only place <c>SaveArgsQueue</c> is dequeued — never runs. <c>SaveHandler.IsSaving</c>
    ///     then latches <c>true</c> forever, and <c>MapState.OnTick</c> returns early before reaching
    ///     <c>Campaign.RealTick</c>, so the campaign stops ticking until the process restarts.
    ///     Nothing is lost by blocking it: the dedicated server's own save goes through
    ///     <c>Game.Save</c> too, so <see cref="SavePatches"/> still publishes <c>GameSaved</c> and the
    ///     paired coop session json is still written.
    ///     </para>
    ///     <para>
    ///     A graphical host keeps the native autosave — nothing else writes its .sav.
    ///     </para>
    /// </summary>
    [HarmonyPatch(typeof(SaveHandler), "SetSaveArgs")]
    internal class SaveHandlerAutoSaveBlockPatch
    {
        internal static bool Prefix() => ModInformation.IsServer && !ModInformation.IsHeadlessHost;
    }
}
