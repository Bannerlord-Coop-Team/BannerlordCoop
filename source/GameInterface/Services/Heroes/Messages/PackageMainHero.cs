using Common.Messaging;
using System;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct PackageMainHero : ICommand
    {
        public Guid TransactionID => throw new NotImplementedException();
    }
}
