using TaleWorlds.Localization;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class TextObjectSerializer : ICustomSerializer
    {
        static Stack<object> Stack = new Stack<object>();
        string text;
        Dictionary<string, object> attributes = new Dictionary<string, object>();
        public TextObjectSerializer(TextObject textObject)
        {
            if (Stack.Contains(textObject))
            {
                return;
            }
            else
            {
                Stack.Push(textObject);
            }

            text = (string)textObject.GetType()
                .GetField("Value", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(textObject);

            if (textObject.Attributes != null)
            {
                foreach (KeyValuePair<string, object> pair in textObject.Attributes)
                {
                    switch (pair.Value)
                    {
                        case TextObject textObj:
                            attributes.Add(pair.Key, new TextObjectSerializer(textObj));
                            break;
                        case string str:
                            attributes.Add(pair.Key, str);
                            break;
                        case int i:
                            attributes.Add(pair.Key, i);
                            break;
                        case float f:
                            attributes.Add(pair.Key, f);
                            break;
                        case null:
                            attributes.Add(pair.Key, null);
                            break;
                        default:
                            string warningMessage = $"Unknown attribute type {pair.Value.GetType()}";
                            LogManager.GetCurrentClassLogger().Warn(warningMessage);
                            break;
                    }
                }
            }

            Stack.Pop();
        }

        public object Deserialize()
        {
            TextObject textObject = new TextObject();
            textObject.GetType()
                .GetField("Value", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(textObject, text);

            textObject.CacheTokens();

            foreach(var attribute in attributes)
            {
                if(attribute.Value is TextObjectSerializer textObjectSerializer)
                {
                    textObject.SetTextVariable(attribute.Key, (TextObject)textObjectSerializer.Deserialize());
                }
                else if(attribute.Value is string str)
                {
                    textObject.SetTextVariable(attribute.Key, str);
                }
                else if (attribute.Value is int i)
                {
                    textObject.SetTextVariable(attribute.Key, i);
                }
                else if (attribute.Value is float f)
                {
                    textObject.SetTextVariable(attribute.Key, f);
                }
                else if (attribute.Value is null)
                {
                    textObject.SetTextVariable(attribute.Key, (string)null);
                }
                else
                {
                    string warningMessage = $"Unknown attribute type {attribute.Value.GetType()}";
                    LogManager.GetCurrentClassLogger().Warn(warningMessage);
                }
                
            }
            

            return textObject;
        }

        public void ResolveReferenceGuids()
        {
            // Do nothing no references exist
        }
    }
}