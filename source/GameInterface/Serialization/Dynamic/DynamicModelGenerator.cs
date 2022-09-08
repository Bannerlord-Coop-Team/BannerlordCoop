using ProtoBuf.Meta;
using System;
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
        ///     Creates a dynamic serialization model for protobuf of the provided type
        /// </summary>
        /// <typeparam name="T">Type to create model</typeparam>
        /// <param name="exclude">Excluded fields by name</param>
        public IMetaTypeContainer CreateDynamicSerializer<T>(string[] exclude = null)
        {
            if (RuntimeTypeModel.Default.CanSerialize(typeof(T)))
                return null;

            FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var selectedFieldNames = fields.Select(f => f.Name).ToArray();
            
            if (exclude != null)
            {
                selectedFieldNames = selectedFieldNames.Except(exclude).ToArray();
                
                if (fields.Length - exclude.Length != selectedFieldNames.Length)
                    throw new Exception($"Some fields are not being used: {string.Join(",", exclude.Except(selectedFieldNames))}");
            }

            var metaType = _typeModel.Add(typeof(T), true).Add(selectedFieldNames);

            metaType.UseConstructor = false;

            return new MetaTypeContainer(metaType);
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
