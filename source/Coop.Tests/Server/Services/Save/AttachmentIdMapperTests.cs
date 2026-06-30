using Common;
using GameInterface.Services.ObjectManager;
using Moq;
using Serilog;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Server.Services.Save;

public class AttachmentIdMapperTests
{
    public AttachmentIdMapperTests()
    {
        // ApplyClientMap is a no-op on the server; these tests exercise the joining-client path.
        ModInformation.IsServer = false;
    }

    [Fact]
    public void ApplyClientMap_ReKeysAttachment_FromDerivedToServerId()
    {
        // Arrange: an attachment registered under the id a joining client re-derives in RegisterAllObjects.
        var objectManager = new ObjectManager(Mock.Of<ILogger>());
        var attachment = new object();
        objectManager.AddExisting("PartyBase_Created_2835", attachment);

        var mapper = new AttachmentIdMapper(objectManager);
        var map = new AttachmentIdMap(new Dictionary<string, string>
        {
            ["PartyBase_Created_2835"] = "PartyBase_Created_3328",
        });

        // Act
        mapper.ApplyClientMap(map);

        // Assert: it now resolves under the server's id, no longer under the re-derived id.
        Assert.True(objectManager.TryGetObject<object>("PartyBase_Created_3328", out var found));
        Assert.Same(attachment, found);
        Assert.False(objectManager.TryGetObject<object>("PartyBase_Created_2835", out _));
    }

    [Fact]
    public void ApplyClientMap_SkipsEntriesWhoseDerivedIdIsNotRegistered()
    {
        var objectManager = new ObjectManager(Mock.Of<ILogger>());
        var mapper = new AttachmentIdMapper(objectManager);
        var map = new AttachmentIdMap(new Dictionary<string, string>
        {
            ["PartyBase_Created_9999"] = "PartyBase_Created_1234",
        });

        // No object is registered under the derived id; applying must not throw or register anything.
        mapper.ApplyClientMap(map);

        Assert.False(objectManager.TryGetObject<object>("PartyBase_Created_1234", out _));
    }

    [Fact]
    public void ApplyClientMap_ReKeysChainedEntries_WithoutOrphaning()
    {
        // Re-derived ids and server ids share the "Created_N" namespace, so one entry's server id can equal
        // another entry's re-derived id. A single-pass Remove+AddExisting orphans one of them depending on
        // dictionary order; the two-phase apply must re-key both regardless.
        var objectManager = new ObjectManager(Mock.Of<ILogger>());
        var partyBaseA = new object();
        var partyBaseB = new object();
        objectManager.AddExisting("PartyBase_Created_5", partyBaseA);
        objectManager.AddExisting("PartyBase_Created_12", partyBaseB);

        var mapper = new AttachmentIdMapper(objectManager);
        var map = new AttachmentIdMap(new Dictionary<string, string>
        {
            ["PartyBase_Created_5"] = "PartyBase_Created_12",   // A's server id is B's re-derived id (the chain)
            ["PartyBase_Created_12"] = "PartyBase_Created_30",
        });

        mapper.ApplyClientMap(map);

        Assert.True(objectManager.TryGetObject<object>("PartyBase_Created_12", out var foundA));
        Assert.Same(partyBaseA, foundA);
        Assert.True(objectManager.TryGetObject<object>("PartyBase_Created_30", out var foundB));
        Assert.Same(partyBaseB, foundB);
    }
}
