using GameInterface.Services.ObjectManager;

namespace E2E.Tests.Environment.Mock
{
    internal class MockObjectManager : IObjectManager
    {
        public bool AddExisting(string id, object obj)
        {
            throw new NotImplementedException();
        }

        public bool AddNewObject(object obj, out string newId)
        {
            throw new NotImplementedException();
        }

        public bool Contains(object obj)
        {
            throw new NotImplementedException();
        }

        public bool Contains(string id)
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

        public bool TryGetId(object obj, out string id)
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
