using GameInterface.Services.ObjectManager;

namespace E2E.Tests.Environment.Mock
{
    internal class MockObjectManager : IObjectManager
    {
        public bool AddExisting<T>(string id, object obj)
        {
            throw new NotImplementedException();
        }

        public bool AddExisting<T>(string id, T obj)
        {
            throw new NotImplementedException();
        }

        public bool AddNewObject<T>(object obj, out string newId)
        {
            throw new NotImplementedException();
        }

        public bool AddNewObject<T>(T obj, out string newId)
        {
            throw new NotImplementedException();
        }

        public bool Contains<T>(object obj)
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

        public bool Remove<T>(object obj)
        {
            throw new NotImplementedException();
        }

        public bool Remove<T>(T obj)
        {
            throw new NotImplementedException();
        }

        public bool TryGetId<T>(object obj, out string id)
        {
            id = default;
            return false;
        }

        public bool TryGetId<T>(T obj, out string id)
        {
            throw new NotImplementedException();
        }
        public bool TryGetId(Type type, object obj, out string id)
        {
            throw new NotImplementedException();
        }

        public bool TryGetObject<T>(string id, out T obj) where T : class
        {
            obj = default;
            return false;
        }

        public bool TryGetObject(Type type, string id, out object obj)
        {
            obj = default;
            return false;
        }

    }
}
