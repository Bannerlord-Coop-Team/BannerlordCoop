using ProtoBuf;
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
    public interface IDynamicModelGenerator
    {
        void AssignSurrogate<TClass, TSurrogate>();
        void Compile();
        IMetaTypeContainer CreateDynamicSerializer<T>(string[] excluded = null);
    }

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
        /// <param name="excluded">Excluded fields by name</param>
        public IMetaTypeContainer CreateDynamicSerializer<T>(string[] excluded = null)
        {
            if (RuntimeTypeModel.Default.CanSerialize(typeof(T)))
                return null;

            FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var selectedFieldNames = fields.Select(f => f.Name).ToArray();
            
            if (excluded != null)
            {
                selectedFieldNames = selectedFieldNames.Except(excluded).ToArray();
                
                if (fields.Length - excluded.Length != selectedFieldNames.Length)
                    throw new Exception($"Some fields are not being used: {string.Join(",", excluded.Except(selectedFieldNames))}");
            }

            var metaType = _typeModel.Add(typeof(T), true).Add(selectedFieldNames);

            metaType.UseConstructor = false;

            return new MetaTypeContainer(metaType);
        }

        public void AssignSurrogate<TClass, TSurrogate>()
        {
            ValidateSurrogate<TClass, TSurrogate>();

            _typeModel.Add(typeof(TClass), false).SetSurrogate(typeof(TSurrogate));
        }

        private void ValidateSurrogate<TClass, TSurrogate>()
        {
            var implicitMethods = typeof(TSurrogate).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "op_Implicit")
                .Where(m =>
                {
                    return (m.ReturnParameter.ParameterType == typeof(TClass) &&
                            m.GetParameters().Length == 1 &&
                            m.GetParameters().First().ParameterType == typeof(TSurrogate)) ||
                            (m.ReturnParameter.ParameterType == typeof(TSurrogate) &&
                            m.GetParameters().Length == 1 &&
                            m.GetParameters().First().ParameterType == typeof(TClass));
                });



            if (implicitMethods.Count() != 2)
            {
                throw new InvalidOperationException($"{typeof(TSurrogate).Name} is not a valid surrogate for {typeof(TClass).Name}");
            }

            if (typeof(TSurrogate).GetCustomAttribute<ProtoContractAttribute>() == null)
            {
                throw new InvalidOperationException($"{typeof(TSurrogate).Name} does not have the {nameof(ProtoContractAttribute)}");
            }
        }

        public void Compile()
        {
            _typeModel.CompileInPlace();
        }
    }
}
