using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    /// <summary>
    /// Property owner objects are serializable.
    /// This class reuses existing serialization functionality.
    /// </summary>
    /// <typeparam name="T">Subclass of PropertyOwner</typeparam>
    [Serializable]
    public class PropertyOwnerSerializer<T> : ICustomSerializer where T : MBObjectBase 
    {
        string data;

        public PropertyOwnerSerializer(PropertyOwner<T> obj)
        {
            StringWriter stringWriter = new StringWriter();
            XmlTextWriter xmlWriter = new XmlTextWriter(stringWriter);
            obj.Serialize(xmlWriter);
            data = stringWriter.ToString();
        }

        public virtual object Deserialize()
        {
            StringReader stringReader = new StringReader(data);
            XmlTextReader xmlReader = new XmlTextReader(stringReader);
            XmlDocument doc = new XmlDocument();
            XmlNode node = doc.ReadNode(xmlReader);
            return node;
        }

        public void ResolveReferenceGuids()
        {
            // No references
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }
}
