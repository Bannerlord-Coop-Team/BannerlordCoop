using GameInterface.Services.Registry;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.AutoSync.Builders;
internal class AutoSyncTestClassRegistry : RegistryBase<AutoSyncTestClass>
{
    public AutoSyncTestClassRegistry(IRegistryCollection collection) : base(collection)
    {
    }

    public override void RegisterAll()
    {
    }

    protected override string GetNewId(AutoSyncTestClass obj)
    {
        return Guid.NewGuid().ToString();
    }
}
