using ProtoBuf.Meta;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Dynamic
{
    public class DynamicModelGenerator : IDynamicModelGenerator
    {
        private readonly RuntimeTypeModel _typeModel;

        internal DynamicModelGenerator()
        {
            _typeModel = RuntimeTypeModel.Default;
        }

        public DynamicModelGenerator(RuntimeTypeModel typeModel)
        {
            _typeModel = typeModel;
        }

        /// <summary>
        /// Creates a dynamic serialization model for protobuf of the provided type
        /// </summary>
        /// <typeparam name="T">Type to create model</typeparam>
        /// <param name="exclude">Excluded fields by name</param>
        public void CreateDynamicSerializer<T>(IEnumerable<string> exclude = null)
        {
            if (RuntimeTypeModel.Default.CanSerialize(typeof(T)))
            {
                return;
            }

            FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (exclude != null)
            {
                HashSet<string> excludesSet = exclude.ToHashSet();
                fields = fields.Where(f => excludesSet.Contains(f.Name) == false).ToArray();
            }

            var objectsFields = fields.Where(f => f.FieldType == typeof(Workshop));
            if (objectsFields.Any())
            {
                throw new Exception("ici");
            }

            string[] fieldNames = fields.Select(f => f.Name).ToArray();
            _typeModel.Add(typeof(T), true).Add(fieldNames);
        }

        public void AssignSurrogate<TClass, TSurrogate>()
        {
            _typeModel.Add(typeof(TClass), false).SetSurrogate(typeof(TSurrogate));
        }

        public void Compile()
        {
            _typeModel.CompileInPlace();
        }
    }
}
