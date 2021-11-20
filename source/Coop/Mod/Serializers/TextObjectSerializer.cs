using TaleWorlds.Localization;
using System.Reflection;
using System;

namespace Coop.Mod.Serializers
{
    [Serializable]
    class TextObjectSerializer : ICustomSerializer
    {

        string text;
        public TextObjectSerializer(TextObject textObject)
        {
            text = (string)textObject.GetType()
                .GetField("Value", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(textObject);
        }

        public object Deserialize()
        {
            TextObject textObject = new TextObject();
            textObject.GetType()
                .GetField("Value", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(textObject, text);

            return textObject;
        }

        public void ResolveReferenceGuids()
        {
            // Do nothing no references exist
        }
    }
}