using Sync;
using TaleWorlds.CampaignSystem.Actions;

namespace Coop.Mod.Patch
{
    /// <summary>
    ///     Patches all public methods call in <see cref="RecruitAction" /> to be synchronized from.
    /// </summary>
    public static class Recruit
    {
        private static readonly MethodPatch Patch =
            new MethodPatch(typeof(RecruitAction)).InterceptAll();

        [PatchInitializer]
        public static void Init()
        {
            CoopClient.Instance.OnPersistenceInitialized += persistence =>
                persistence.RpcSyncHandlers.Register(Patch.Methods, CoopClient.Instance);
            foreach (MethodAccess method in Patch.Methods)
            {
                method.Condition = Coop.DoSync;
            }
        }
    }
}
