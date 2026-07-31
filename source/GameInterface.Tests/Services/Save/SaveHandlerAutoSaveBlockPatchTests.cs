using Common;
using GameInterface.Services.Save.Patches;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Save;

/// <summary>
/// Tests the native save scheduler block. Only a graphical host may run the native autosave:
/// clients are not authoritative, and a headless host already drives its own save schedule —
/// letting both run overwrites the single-slot pending-save callback in Game, latching
/// SaveHandler.IsSaving true and freezing campaign time for the rest of the process
/// (<see href="https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/2540">issue #2540</see>).
/// <para>
/// Mutates the process-wide role and BANNERLORD_USER_DIR, so it joins the serialized role collection.
/// </para>
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class SaveHandlerAutoSaveBlockPatchTests : IDisposable
{
    private const string HeadlessMarker = "BANNERLORD_USER_DIR";

    private readonly bool wasServer;
    private readonly string? wasUserDir;

    public SaveHandlerAutoSaveBlockPatchTests()
    {
        wasServer = ModInformation.IsServer;
        wasUserDir = Environment.GetEnvironmentVariable(HeadlessMarker);
    }

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
        Environment.SetEnvironmentVariable(HeadlessMarker, wasUserDir);
    }

    [Fact]
    public void GraphicalHost_RunsNativeAutoSave()
    {
        ModInformation.IsServer = true;
        Environment.SetEnvironmentVariable(HeadlessMarker, null);

        Assert.True(SaveHandlerAutoSaveBlockPatch.Prefix());
    }

    [Fact]
    public void HeadlessHost_BlocksNativeAutoSave()
    {
        ModInformation.IsServer = true;
        Environment.SetEnvironmentVariable(HeadlessMarker, @"C:\server-data");

        Assert.False(SaveHandlerAutoSaveBlockPatch.Prefix());
    }

    [Fact]
    public void Client_BlocksNativeAutoSave_RegardlessOfHeadlessMarker()
    {
        ModInformation.IsServer = false;

        Environment.SetEnvironmentVariable(HeadlessMarker, null);
        Assert.False(SaveHandlerAutoSaveBlockPatch.Prefix());

        Environment.SetEnvironmentVariable(HeadlessMarker, @"C:\server-data");
        Assert.False(SaveHandlerAutoSaveBlockPatch.Prefix());
    }

    /// <summary>An empty value is not a marker — it is how the variable reads when never set.</summary>
    [Fact]
    public void EmptyHeadlessMarker_CountsAsGraphical()
    {
        ModInformation.IsServer = true;
        Environment.SetEnvironmentVariable(HeadlessMarker, "");

        Assert.False(ModInformation.IsHeadlessHost);
        Assert.True(SaveHandlerAutoSaveBlockPatch.Prefix());
    }
}
