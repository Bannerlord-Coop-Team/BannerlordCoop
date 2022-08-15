using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using TaleWorlds.Localization;

namespace GameInterface.Serialization.Models
{
    [ProtoContract(SkipConstructor = true)]
    public class TextObjectSurrogate
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
