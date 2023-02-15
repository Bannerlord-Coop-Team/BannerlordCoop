using Missions.Services.Network.Surrogates;
using ProtoBuf.Meta;
using TaleWorlds.Library;

namespace IntroductionServerTests
{
    public sealed class RunTimeHelper
    {
        private static RunTimeHelper _instance;

        public static RunTimeHelper Instance
        {
            get {
                if (_instance == null)
                {
                    _instance = new RunTimeHelper();
                    _instance.SetupSurrogates();
                }

                return Instance;
            }
        }

        private void SetupSurrogates()
        {        
            RuntimeTypeModel.Default.SetSurrogate<Vec3, Vec3Surrogate>();
            RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>();            
        }
    }
}
