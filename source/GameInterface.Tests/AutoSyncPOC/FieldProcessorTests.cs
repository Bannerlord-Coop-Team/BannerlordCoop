using Common.Network;
using GameInterface.AutoSync;
using GameInterface.AutoSyncPOC;
using GameInterface.AutoSyncPOC.Mapper;
using GameInterface.AutoSyncPOC.Messages;
using GameInterface.Tests.AutoSyncPOC.TestClasses;
using HarmonyLib;
using Moq;
using Serilog;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.AutoSyncPOC;

public class FieldProcessorTests
{
    private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
    private readonly Mock<INetwork> _networkMock = new Mock<INetwork>();

    private readonly IAutoSyncFieldMapper _fieldMapper;
    private readonly INetworkIdRegistry _networkIdRegistry;
    private readonly IFieldRegistry _fieldRegistry;
    private readonly IFieldProcessor _fieldProcessor;
    public FieldProcessorTests()
    {
        _networkIdRegistry = new NetworkIdRegistry();
        _fieldRegistry = new FieldRegistry(_loggerMock.Object);

        _fieldMapper = new AutoSyncFieldMapper(
            _networkIdRegistry,
            _fieldRegistry
        );

        _fieldProcessor = new FieldProcessor(
            _loggerMock.Object,
            _networkMock.Object,
            _fieldRegistry,
            _fieldMapper,
            _networkIdRegistry
        );
    }

    [Fact]
    public void SendValue_SendsSetFieldValueCommand_ForNonNullValue()
    {
        // Arrange
        var field = AccessTools.Field(typeof(FieldClass), "MyNullableField");

        var instance = new FieldClass(fieldValue: 0, nullableField: null);
        int assignValue = 55;

        Assert.Null(instance.GetNullableField());

        // Register instance and field info
        Assert.True(_fieldRegistry.TryAddField(field));
        Assert.True(_networkIdRegistry.TryRegisterObject(instance, out var networkId));

        // Get field map (maps integers to instance and fields)
        Assert.True(_fieldMapper.TryGetFieldMap(instance, field, out var fieldMap));

        // Act
        _fieldProcessor.SendValue(instance, fieldMap.FieldId, assignValue);

        // Assert
        _networkMock.Verify(x => x.SendAll(It.Is<SetFieldCommand>(cmd =>
            cmd.FieldMap.NetworkId == fieldMap.NetworkId &&
            cmd.FieldMap.FieldId == fieldMap.FieldId &&
            cmd.SerializedValue.SequenceEqual(RawSerializer.Serialize(assignValue))
        )));
    }

    [Fact]
    public void SendValue_SendsSetFieldValueNull_ForNullValue()
    {
        // Arrange
        var field = AccessTools.Field(typeof(FieldClass), "MyNullableField");

        var instance = new FieldClass(fieldValue: 0, nullableField: 5000);
        int? assignValue = null;

        Assert.NotNull(instance.GetNullableField());

        // Register instance and field info
        Assert.True(_fieldRegistry.TryAddField(field));
        Assert.True(_networkIdRegistry.TryRegisterObject(instance, out var networkId));

        // Get field map (maps integers to instance and fields)
        Assert.True(_fieldMapper.TryGetFieldMap(instance, field, out var fieldMap));

        // Act
        _fieldProcessor.SendValue(instance, fieldMap.FieldId, assignValue);

        // Assert
        _networkMock.Verify(x => x.SendAll(It.Is<SetFieldNull>(cmd =>
            cmd.FieldMap.NetworkId == fieldMap.NetworkId &&
            cmd.FieldMap.FieldId == fieldMap.FieldId
        )));
    }

    [Fact]
    public void SendValue_SendsSetFieldRefCommand_ForNonNullValue()
    {
        // Arrange
        var field = AccessTools.Field(typeof(FieldClass), "MyRefField");

        var refClass = new RefClass();
        var instance = new FieldClass(fieldValue: 0, nullableField: null, refField: null);

        Assert.Null(instance.GetNullableField());

        // Register instance and field info
        Assert.True(_fieldRegistry.TryAddField(field));
        Assert.True(_networkIdRegistry.TryRegisterObject(instance, out var networkId));
        Assert.True(_networkIdRegistry.TryRegisterObject(refClass, out var fieldNetworkId));

        // Get field map (maps integers to instance and fields)
        Assert.True(_fieldMapper.TryGetFieldMap(instance, field, out var fieldMap));

        // Act
        _fieldProcessor.SendReference(instance, fieldMap.FieldId, refClass);

        // Assert
        _networkMock.Verify(x => x.SendAll(It.Is<SetFieldCommand>(cmd =>
            cmd.FieldMap.NetworkId == fieldMap.NetworkId &&
            cmd.FieldMap.FieldId == fieldMap.FieldId &&
            cmd.SerializedValue.SequenceEqual(RawSerializer.Serialize(fieldNetworkId))
        )));
    }

    [Fact]
    public void SendValue_SendsSetFieldRefNull_ForNullValue()
    {
        // Arrange
        var field = AccessTools.Field(typeof(FieldClass), "MyRefField");

        var refClass = new RefClass();
        var instance = new FieldClass(fieldValue: 0, nullableField: null, refField: refClass);

        Assert.NotNull(instance.GetRefField());

        // Register instance and field info
        Assert.True(_fieldRegistry.TryAddField(field));
        Assert.True(_networkIdRegistry.TryRegisterObject(instance, out var networkId));
        Assert.True(_networkIdRegistry.TryRegisterObject(refClass, out var _));

        // Get field map (maps integers to instance and fields)
        Assert.True(_fieldMapper.TryGetFieldMap(instance, field, out var fieldMap));

        // Act
        _fieldProcessor.SendReference<FieldClass, RefClass?>(instance, fieldMap.FieldId, null);

        // Assert
        _networkMock.Verify(x => x.SendAll(It.Is<SetFieldNull>(cmd =>
            cmd.FieldMap.NetworkId == fieldMap.NetworkId &&
            cmd.FieldMap.FieldId == fieldMap.FieldId
        )));
    }
}
