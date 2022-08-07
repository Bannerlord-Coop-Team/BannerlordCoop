using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Data
{
    public class HeroWrapper : WrapperBase<Hero>
    {
        public static event Action<HeroWrapper> OnHeroCreated;

               

        internal HeroWrapper(Hero hero) : base(hero) { OnHeroCreated?.Invoke(this); }

        internal HeroWrapper(Guid guid) : base(guid) { }


        private static readonly FieldInfo characterObjectFieldInfo = typeof(CharacterObject)
            .GetField("_characterObject", BindingFlags.Instance | BindingFlags.NonPublic);
        public CharacterObjectWrapper Character
        {
            get 
            {
                return new CharacterObjectWrapper(Object.CharacterObject);
            }
            set
            {
                characterObjectFieldInfo.SetValue(Object, value.Object);
            }
        }
    }
}
