using Common.Logging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemObjects.Interfaces;
public interface IItemObjectInterface : IGameAbstraction
{
    byte[] PackageItemObject(ItemObject itemObject);

    ItemObject UnpackItemObject(byte[] bytes);
}

internal class ItemObjectInterface : IItemObjectInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<ItemObjectInterface>();
    private readonly IObjectManager objectManager;
    private readonly IBinaryPackageFactory binaryPackageFactory;

    public ItemObjectInterface(
        IBinaryPackageFactory binaryPackageFactory,
        IObjectManager objectManager)
    {
        this.objectManager = objectManager;
        this.binaryPackageFactory = binaryPackageFactory;
        this.objectManager = objectManager;
    }

    public byte[] PackageItemObject(ItemObject itemObject)
    {
        ItemObjectBinaryPackage package = binaryPackageFactory.GetBinaryPackage<ItemObjectBinaryPackage>(itemObject);
        return BinaryFormatterSerializer.Serialize(package);
    }

    public ItemObject UnpackItemObject(byte[] bytes)
    {
        ItemObjectBinaryPackage package = BinaryFormatterSerializer.Deserialize<ItemObjectBinaryPackage>(bytes);
        return package.Unpack<ItemObject>(binaryPackageFactory);
    }
}