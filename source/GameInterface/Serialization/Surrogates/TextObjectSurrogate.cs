using ProtoBuf;
using System.Reflection;
using JetBrains.Annotations;
using TaleWorlds.Localization;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class TextObjectSurrogate : ISurrogate
    {
        [ProtoMember(1)]
        string Value { get; }

        // TODO attributes

        private static readonly FieldInfo info_Value = typeof(TextObject).GetField("Value", BindingFlags.NonPublic | BindingFlags.Instance);

        private TextObjectSurrogate([NotNull] TextObject obj)
        {
            Value = (string)info_Value.GetValue(obj);
        }

        private TextObject Deserialize()
        {
            return new TextObject(Value);
        }

        public static implicit operator TextObjectSurrogate(TextObject obj)
        {
            if(obj == null) return null;
            return new TextObjectSurrogate(obj);
        }

        public static implicit operator TextObject(TextObjectSurrogate surrogate)
        {
            if(surrogate == null) return null;
            return surrogate.Deserialize();
        }
    }
}
