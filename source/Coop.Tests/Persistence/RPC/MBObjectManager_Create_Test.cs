using System.Linq;
using Sync;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace Coop.Tests.Persistence.RPC
{
    [Collection(
        "UsesGlobalPatcher")] // Need be executed sequential since harmony patches are always global
    public class MBObjectManager_Create_Test
    {
        private static readonly MethodPatch Patch =
            new MethodPatch(typeof(MBObjectManager)).InterceptGeneric(
                nameof(MBObjectManager.CreateObject),
                new[] {typeof(MobileParty)});

        [Fact]
        private void CanPatchBeCreated()
        {
            // Verify generated patch
            Assert.True(
                Patch.TryGetMethod(Patch.Methods.First().MemberInfo, out MethodAccess method));
            Assert.True(method.MemberInfo.IsGenericMethod);
            Assert.Equal(new[] {typeof(MobileParty)}, method.MemberInfo.GetGenericArguments());
            Assert.True(MethodRegistry.MethodToId.ContainsKey(method));
        }
    }
}
