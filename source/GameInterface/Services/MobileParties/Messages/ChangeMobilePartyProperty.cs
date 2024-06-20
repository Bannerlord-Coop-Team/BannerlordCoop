using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.MobileParties.Messages
{
    public record ChangeMobilePartyProperty(int PropertyType, string Value1, string Value2, string Value3 = null) : ICommand
    {
        public int PropertyType { get; } = PropertyType;
        public string Value1 { get; } = Value1;
        public string Value2 { get; } = Value2;
        public string Value3 { get; } = Value3;
    }
}
