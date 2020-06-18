using System.Reflection;
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
            new MethodPatch(typeof(RecruitAction)).InterceptAll(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);

        [PatchInitializer]
        public static void Init()
        {
            CoopClient.Instance.OnPersistenceInitialized += persistence =>
                persistence.RpcSyncHandlers.Register(Patch.Methods);
            foreach (MethodAccess method in Patch.Methods)
            {
                method.Condition = Coop.DoSync;
            }
        }
    }
}
