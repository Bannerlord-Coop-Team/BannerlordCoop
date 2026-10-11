using GameInterface.Registry.Auto;
using GameInterface.Services.Ships;
using GameInterface.Tests.Bootstrap;
using Moq;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;
using ObjectManagerService = GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Tests.Services.Ships;

public class ShipCatalogRegistryTests
{
    [Fact]
    public void RegisterAllObjects_RegistersEachCatalogObjectUnderItsStringId()
    {
        GameBootStrap.Initialize();
        var suffix = Guid.NewGuid().ToString("N");
        var hull = MBObjectManager.Instance.RegisterObject(new ShipHull { StringId = "test_hull_" + suffix });
        var piece = MBObjectManager.Instance.RegisterObject(new ShipUpgradePiece { StringId = "test_piece_" + suffix });
        var figurehead = MBObjectManager.Instance.RegisterObject(new Figurehead("test_figurehead_" + suffix));
        try
        {
            var manager = new ObjectManagerService(Mock.Of<ILogger>());
            new ShipHullRegistry(Mock.Of<ILogger>(), Mock.Of<IAutoRegistryFactory>(), manager).RegisterAllObjects();
            new ShipUpgradePieceRegistry(Mock.Of<ILogger>(), Mock.Of<IAutoRegistryFactory>(), manager).RegisterAllObjects();
            new FigureheadRegistry(Mock.Of<ILogger>(), Mock.Of<IAutoRegistryFactory>(), manager).RegisterAllObjects();

            Assert.True(manager.TryGetId(hull, out var hullId));
            Assert.True(manager.TryGetId(piece, out var pieceId));
            Assert.True(manager.TryGetId(figurehead, out var figureheadId));
            Assert.Equal("ShipHull_" + hull.StringId, hullId);
            Assert.Equal("ShipUpgradePiece_" + piece.StringId, pieceId);
            Assert.Equal("Figurehead_" + figurehead.StringId, figureheadId);
        }
        finally
        {
            MBObjectManager.Instance.UnregisterObject(figurehead);
            MBObjectManager.Instance.UnregisterObject(piece);
            MBObjectManager.Instance.UnregisterObject(hull);
        }
    }

    [Fact]
    public void RegisterAllObjects_FigureheadResolvesFromItsBareStringId()
    {
        GameBootStrap.Initialize();
        var figurehead = MBObjectManager.Instance.RegisterObject(new Figurehead("test_figurehead_" + Guid.NewGuid().ToString("N")));
        try
        {
            var manager = new ObjectManagerService(Mock.Of<ILogger>());
            new FigureheadRegistry(Mock.Of<ILogger>(), Mock.Of<IAutoRegistryFactory>(), manager).RegisterAllObjects();

            // NavalNotificationsHandler sends Figurehead.StringId and resolves it this way.
            Assert.True(manager.TryGetObject<Figurehead>(figurehead.StringId, out var resolved));
            Assert.Same(figurehead, resolved);
        }
        finally
        {
            MBObjectManager.Instance.UnregisterObject(figurehead);
        }
    }
}
