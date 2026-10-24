#if DEBUG
using Common;
using GameInterface.Services.StanceLinks.Commands;
using Serilog;
using System;
using Xunit;

namespace GameInterface.Tests.Services.StanceLinks;

[Collection(ModInformationRoleCollection.Name)]
public class StanceLinkHandleDebugCommandTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    public void Dispose() => ModInformation.IsServer = wasServer;

    [Fact]
    public void Setup_RemovesOnlyNumericIdentity_AndRestoresSameInstance()
    {
        ModInformation.IsServer = false;
        var manager = new global::GameInterface.Services.ObjectManager.ObjectManager(Log.Logger);
        var stance = new object();
        var other = new object();
        Assert.True(manager.AddExisting("stance", stance, 40854));
        Assert.True(manager.AddExisting("other", other, 40855));

        StanceLinkHandleDebugCommand.RemoveHandleForSetup(manager, stance, 40854);

        Assert.True(manager.TryGetObject<object>("stance", out var byString));
        Assert.Same(stance, byString);
        Assert.False(manager.TryGetHandle(stance, out _));
        Assert.False(manager.TryGetObject<object>(40854, out _));
        Assert.True(manager.TryGetObject<object>(40855, out var unchanged));
        Assert.Same(other, unchanged);
        Assert.True(manager.AddExisting("stance", stance, 40854));
        Assert.True(manager.AddExisting("stance", stance, 40854));
        Assert.True(manager.TryGetObject<object>(40854, out var byHandle));
        Assert.Same(stance, byHandle);
    }

    [Fact]
    public void Setup_OnServer_LeavesRegistryUnchanged()
    {
        ModInformation.IsServer = true;
        var manager = new global::GameInterface.Services.ObjectManager.ObjectManager(Log.Logger);
        var stance = new object();
        Assert.True(manager.AddExisting("stance", stance, 40854));
        Assert.Throws<InvalidOperationException>(() => StanceLinkHandleDebugCommand.RemoveHandleForSetup(manager, stance, 40854));
        Assert.True(manager.TryGetObject<object>(40854, out var found));
        Assert.Same(stance, found);
    }
}
#endif
