using Common.Serialization;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Common.Tests.Serialization;

public class ProtoBufSerializerTests
{
    [ProtoContract(SkipConstructor = true)]
    public readonly struct SkipConstructorStruct
    {
        [ProtoMember(1)]
        public readonly int Number;

        [ProtoMember(2)]
        public readonly string Text;

        public SkipConstructorStruct(int number, string text)
        {
            Number = number;
            Text = text;
        }
    }

    [ProtoContract(SkipConstructor = true)]
    public class SkipConstructorClass
    {
        [ProtoMember(1)]
        public int Number { get; }

        public SkipConstructorClass(int number)
        {
            Number = number;
        }
    }

    [Fact]
    public void ConfigureRuntimeModel_KeepsCompilationAndUsesDefaultInitializationForStructs()
    {
        var model = RuntimeTypeModel.Create();
        ProtoBufSerializer.ConfigureRuntimeModel(model, isMonoRuntime: true);
        var metaType = model.Add(typeof(SkipConstructorStruct), applyDefaultBehaviour: true);

        Assert.True(model.AutoCompile);
        Assert.True(metaType.UseConstructor);

        var expected = new SkipConstructorStruct(42, "linux");
        using var stream = new MemoryStream();
        model.Serialize(stream, expected);
        stream.Position = 0;

        var actual = (SkipConstructorStruct)model.Deserialize(
            stream,
            value: null,
            typeof(SkipConstructorStruct));

        Assert.Equal(expected.Number, actual.Number);
        Assert.Equal(expected.Text, actual.Text);
    }

    [Fact]
    public void ConfigureRuntimeModel_DoesNotChangeReferenceTypeConstruction()
    {
        var model = RuntimeTypeModel.Create();
        ProtoBufSerializer.ConfigureRuntimeModel(model, isMonoRuntime: true);
        var metaType = model.Add(typeof(SkipConstructorClass), applyDefaultBehaviour: true);

        Assert.False(metaType.UseConstructor);
    }

    [Fact]
    public void ConfigureRuntimeModel_DoesNotChangeWindowsStructConstruction()
    {
        var model = RuntimeTypeModel.Create();
        ProtoBufSerializer.ConfigureRuntimeModel(model, isMonoRuntime: false);
        var metaType = model.Add(typeof(SkipConstructorStruct), applyDefaultBehaviour: true);

        Assert.True(model.AutoCompile);
        Assert.False(metaType.UseConstructor);
    }
}
