using System;
using HarmonyLib;
using TaleWorlds.Library;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// The native engine owns the on-screen inquiry UI; headless there is nothing to display a
    /// <see cref="InformationManager.ShowInquiry"/> dialog or feed it an answer, so any code that
    /// blocks on one (e.g. <c>SandBoxSaveHelper.TryLoadSave</c>'s save-compatibility "load anyway?"
    /// prompt when a save predates a game update) would stall forever — the affirmative callback that
    /// actually starts the load never runs.
    ///
    /// Auto-accept every inquiry: log its title/text (so the compatibility warning is visible) and
    /// invoke the affirmative action inline, mirroring an operator clicking "Continue".
    /// </summary>
    [HarmonyPatch(typeof(InformationManager))]
    internal class InquiryAutoAcceptPatches
    {
        [HarmonyPatch(nameof(InformationManager.ShowInquiry))]
        [HarmonyPrefix]
        static bool ShowInquiryPrefix(InquiryData data)
        {
            Console.WriteLine($"[ServerHeadless] Inquiry auto-accepted: \"{data?.TitleText}\" — {data?.Text}");
            data?.AffirmativeAction?.Invoke();
            return false; // skip the native UI
        }
    }
}
