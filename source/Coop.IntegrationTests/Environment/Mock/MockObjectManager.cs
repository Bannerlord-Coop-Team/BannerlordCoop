using GameInterface.Services.ObjectManager;

namespace Coop.IntegrationTests.Environment.Mock
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

        public bool TryGetId(object obj, out string id)
        {
            throw new NotImplementedException();
        }

        public bool TryGetObject<T>(string id, out T obj)
        {
            throw new NotImplementedException();
        }
    }
}
