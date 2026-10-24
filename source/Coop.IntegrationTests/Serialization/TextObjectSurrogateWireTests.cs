using Common.Messaging;
using Common.Serialization;
using GameInterface.Surrogates;
using ProtoBuf;
using TaleWorlds.Localization;

namespace Coop.IntegrationTests.Serialization;

public class TextObjectSurrogateWireTests
{
    private readonly ProtoBufSerializer serializer;

    public TextObjectSurrogateWireTests()
    {
        _ = new SurrogateCollection();
        serializer = new ProtoBufSerializer(new SerializableTypeMapper());
    }

    [Theory]
    [InlineData(7, 0)]
    [InlineData(7, 1)]
    [InlineData(7, 2)]
    [InlineData(8, 0)]
    [InlineData(8, 1)]
    [InlineData(8, 2)]
    public void RegisteredSerializer_PreservesAttributeContainer(int field, int state)
    {
        var text = CreateText(state);
        var message = new JournalTextMessage
        {
            LogText = field == 7 ? text : null,
            TaskName = field == 8 ? text : null,
        };

        var copy = RoundTrip(message);
        var actual = field == 7 ? copy.LogText : copy.TaskName;

        AssertText(text, actual);
        Assert.True(text.Equals(actual));
        Assert.Null(field == 7 ? copy.TaskName : copy.LogText);
    }

    [Fact]
    public void RegisteredSerializer_PreservesRecursiveTemplatesAndTypedAttributes()
    {
        var templateValue = new TextObject("{=task}Deliver {COUNT} {SUBJECT}", new Dictionary<string, object>
        {
            ["COUNT"] = 12,
            ["SUBJECT"] = new TextObject("{=subject}{NULL_CONTAINER} {EMPTY_CONTAINER}", new Dictionary<string, object>
            {
                ["NULL_CONTAINER"] = CreateText(0),
                ["EMPTY_CONTAINER"] = CreateText(1),
                ["NULL_KEY"] = CreateText(2),
            }),
            ["NAME"] = "Danustica",
            ["ZERO"] = 0f,
            ["FRACTION"] = -1.25f,
            ["NULL"] = null,
        });

        var copy = RoundTrip(new JournalTextMessage { LogText = templateValue, TaskName = templateValue });

        AssertText(templateValue, copy.LogText);
        AssertText(templateValue, copy.TaskName);
    }

    [Fact]
    public void RegisteredSerializer_DistinguishesNullAndEmptyContainersOnWire()
    {
        var nullContainer = new JournalTextMessage { TaskName = CreateText(0) };
        var emptyContainer = new JournalTextMessage { TaskName = CreateText(1) };

        Assert.False(nullContainer.TaskName.Equals(emptyContainer.TaskName));
        Assert.NotEqual(serializer.Serialize(nullContainer), serializer.Serialize(emptyContainer));
    }

    private static TextObject CreateText(int state)
    {
        return new TextObject("{=task}Task name", state switch
        {
            0 => null,
            1 => new Dictionary<string, object>(),
            _ => new Dictionary<string, object> { ["KINGDOM_NAME"] = null },
        });
    }

    private JournalTextMessage RoundTrip(JournalTextMessage message)
    {
        var bytes = serializer.Serialize(message);
        var copy = Assert.IsType<JournalTextMessage>(serializer.Deserialize<IMessage>(bytes));
        Assert.Equal(bytes, serializer.Serialize(copy));
        return copy;
    }

    private static void AssertText(TextObject expected, TextObject actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Value, actual.Value);
        if (expected.Attributes == null)
        {
            Assert.Null(actual.Attributes);
            return;
        }

        Assert.NotNull(actual.Attributes);
        Assert.Equal(expected.Attributes.Count, actual.Attributes.Count);
        foreach (var attribute in expected.Attributes)
        {
            Assert.True(actual.Attributes.ContainsKey(attribute.Key));
            var value = actual.Attributes[attribute.Key];
            if (attribute.Value is TextObject nested)
                AssertText(nested, Assert.IsType<TextObject>(value));
            else
            {
                Assert.Equal(attribute.Value?.GetType(), value?.GetType());
                Assert.Equal(attribute.Value, value);
            }
        }
    }

    // The journal on this branch predates its TextObject fields; exercise those tags without changing it.
    [ProtoContract]
    public class JournalTextMessage : IMessage
    {
        [ProtoMember(7)] public TextObject LogText { get; set; }
        [ProtoMember(8)] public TextObject TaskName { get; set; }
    }
}
