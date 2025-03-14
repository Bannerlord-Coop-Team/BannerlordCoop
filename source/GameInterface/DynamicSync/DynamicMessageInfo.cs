using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace GameInterface.DynamicSync
{
    public class DynamicMessageInfo
    {
        public string MessageName { get; set; }

        public string MemberName { get; set; }

        public Type ClassType { get; set; }

        public Type MemberType { get; set; }

        public DynamicMessageType Type { get; set; }

        public DynamicMessageAction Action { get; set; }
        public List<string> UsingDeclarations { get; set; } = new List<string>();
    }
}
