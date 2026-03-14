using Common.Messaging;
using GameInterface.AutoSync;
using GameInterface.AutoSyncPOC;
using GameInterface.AutoSyncPOC.Handlers;
using GameInterface.AutoSyncPOC.Mapper;
using GameInterface.AutoSyncPOC.Messages;
using GameInterface.Tests.AutoSyncPOC.TestClasses;
using HarmonyLib;
using Moq;
using Serilog;
using Xunit;

namespace GameInterface.Tests.AutoSyncPOC;

public class AutoSyncFieldHanderTests
{
    private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
    private readonly Mock<IMessageBroker> _messageBrokerMock = new Mock<IMessageBroker>();

    private readonly IAutoSyncFieldMapper _fieldMapper;
    private readonly INetworkIdRegistry _networkIdRegistry;
    private readonly IFieldRegistry _fieldRegistry;
    private readonly AutoSyncFieldHandler _handler;
    public AutoSyncFieldHanderTests()
    {
        _networkIdRegistry = new NetworkIdRegistry();
        _fieldRegistry = new FieldRegistry(_loggerMock.Object);

        _fieldMapper = new AutoSyncFieldMapper(
            _networkIdRegistry,
            _fieldRegistry
        );

        _handler = new AutoSyncFieldHandler(
            _loggerMock.Object,
            _messageBrokerMock.Object,
            _fieldMapper,
            _networkIdRegistry,
            _fieldRegistry
        );
    }

    [Fact]
    public void FieldHandler_HandleSetFieldCommand_UpdatesTargetField()
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

        _handler.Handle_FieldSetCommand(new MessagePayload<SetFieldCommand>(null, recievedMessage));

        // Assert
        Assert.Equal(assignValue, instance.GetField());

        // Verify no errors occurred
        _loggerMock.Verify(
            logger => logger.Error(It.IsAny<string>()),
            Times.Never);

        _loggerMock.Verify(
            logger => logger.Error(It.IsAny<string>(), It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public void FieldHandler_HandleSetNullCommand_UpdatesTargetFieldToNull()
    {
        // Arrange
        var field = AccessTools.Field(typeof(FieldClass), "MyNullableField");

        var instance = new FieldClass(fieldValue: 0, nullableField: 5000);

        Assert.NotNull(instance.GetNullableField());

        // Register instance and field info
        Assert.True(_fieldRegistry.TryAddField(field));
        Assert.True(_networkIdRegistry.TryRegisterObject(instance, out var networkId));

        // Get field map (maps integers to instance and fields)
        Assert.True(_fieldMapper.TryGetFieldMap(instance, field, out var fieldMap));

        // Create message and serialize it
        var messageToSend = new SetFieldNull(fieldMap);
        var bytes = RawSerializer.Serialize(messageToSend);

        // Act
        // Deserialize message
        var recievedMessage = RawSerializer.Deserialize<SetFieldNull>(bytes);

        _handler.Handle_FieldSetNullCommand(new MessagePayload<SetFieldNull>(null, recievedMessage));

        // Assert
        Assert.Null(instance.GetNullableField());

        // Verify no errors occurred
        _loggerMock.Verify(
            logger => logger.Error(It.IsAny<string>()),
            Times.Never);

        _loggerMock.Verify(
            logger => logger.Error(It.IsAny<string>(), It.IsAny<object[]>()),
            Times.Never);
    }

    [Fact]
    public void FieldHandler_HandleSetFieldCommand_UpdatesNullToValue()
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

        // Create message and serialize it
        var messageToSend = new SetFieldCommand(fieldMap, RawSerializer.Serialize(assignValue));
        var bytes = RawSerializer.Serialize(messageToSend);

        // Act
        // Deserialize message
        var recievedMessage = RawSerializer.Deserialize<SetFieldCommand>(bytes);

        _handler.Handle_FieldSetCommand(new MessagePayload<SetFieldCommand>(null, recievedMessage));

        // Assert
        Assert.Equal(assignValue, instance.GetNullableField());

        // Verify no errors occurred
        _loggerMock.Verify(
            logger => logger.Error(It.IsAny<string>()),
            Times.Never);

        _loggerMock.Verify(
            logger => logger.Error(It.IsAny<string>(), It.IsAny<object[]>()),
            Times.Never);
    }
}
