using GameInterface.Services.ObjectManager;
using TaleWorlds.ObjectSystem;

namespace Coop.IntegrationTests.Environment.Mock
{
    internal class MockObjectManager : IObjectManager
    {

        public bool AddExisting<T>(string id, T obj)
        {
            throw new NotImplementedException();
        }

        public bool AddNewObject<T>(T obj, out string newId)
        {
            throw new NotImplementedException();
        }

        public bool Contains(string id)
        {
            throw new NotImplementedException();
        }

        public bool Contains<T>(T obj)
        {
            throw new NotImplementedException();
        }

        public bool IsTypeManaged(Type type)
        {
            throw new NotImplementedException();
        }

        public bool Remove(object obj)
        {
            throw new NotImplementedException();
        }

        public bool Remove<T>(T obj)
        {
            throw new NotImplementedException();
        }

        public bool TryGetId<T>(T obj, out string id)
        {
            id = default;
            return false;
        }

        public bool TryGetObject<T>(string id, out T obj) where T : class
        {
            obj = default;
            return false;
        }
    }
}
