using GameInterface.AutoSync;
using GameInterface.AutoSyncPOC;
using GameInterface.AutoSyncPOC.Mapper;
using GameInterface.AutoSyncPOC.Messages;
using GameInterface.Tests.AutoSyncPOC.TestClasses;
using HarmonyLib;
using Moq;
using Serilog;
using Xunit;

namespace GameInterface.Tests.AutoSyncPOC;

public class AutoSyncFieldMapperTests
{
    private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
    private readonly IAutoSyncFieldMapper _fieldMapper;
    private readonly INetworkIdRegistry _networkIdRegistry;
    private readonly IFieldRegistry _fieldRegistry;
    public AutoSyncFieldMapperTests()
    {
        _networkIdRegistry = new NetworkIdRegistry();
        _fieldRegistry = new FieldRegistry(_loggerMock.Object);

        _fieldMapper = new AutoSyncFieldMapper(
            _networkIdRegistry,
            _fieldRegistry
        );
    }

    [Fact]
    public void FieldMapper_ShouldResolveFieldAndApplySerializedValue()
    {
        // Arrange
        var field = AccessTools.Field(typeof(FieldClass), "MyField");

        var instance = new FieldClass(fieldValue: 0); 
        int assignValue = 55;

        // Register instance and field info
        Assert.True(_fieldRegistry.TryAddField(field));
        Assert.True(_networkIdRegistry.TryRegisterObject(instance, out var networkId));

        // Get field map (maps integers to instance and fields)
        Assert.True(_fieldMapper.TryGetFieldMap(instance, field, out var fieldMap));

        // Create message and serialize it
        var messageToSend = new SetFieldCommand(fieldMap, RawSerializer.Serialize(assignValue));
        var bytes = RawSerializer.Serialize(messageToSend);

        // Act
        // Deserialize message
        var recievedMessage = RawSerializer.Deserialize<SetFieldCommand>(bytes);
        var recievedValue = RawSerializer.Deserialize<int>(recievedMessage.SerializedValue);

        // Resolve instance and fieldinfo
        Assert.True(_networkIdRegistry.TryGetObject(recievedMessage.FieldMap.NetworkId, out var recievedInstance));
        Assert.True(_fieldRegistry.TryGetField(recievedMessage.FieldMap.FieldId, out var recievedField));

        // Set value
        recievedField.SetValue(recievedInstance, recievedValue);

        // Assert
        Assert.Equal(instance, recievedInstance);
        Assert.Equal(field, recievedField);
        Assert.Equal(assignValue, instance.GetField());

        // Verify no errors occurred
        _loggerMock.Verify(
            logger => logger.Error(It.IsAny<string>()),
            Times.Never);

        _loggerMock.Verify(
            logger => logger.Error(It.IsAny<string>(), It.IsAny<object[]>()),
            Times.Never);
    }
}
