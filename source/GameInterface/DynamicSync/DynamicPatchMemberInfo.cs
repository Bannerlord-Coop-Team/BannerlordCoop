using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync
{
    public class DynamicPatchMemberInfo
    {
        public MemberInfo MemberInfo { get; set; }

        public DynamicTranspilerType TranspilerType { get; set; }

        public List<DynamicMessageInfo> MessageInfos { get; set; } = new List<DynamicMessageInfo>();

        public HashSet<string> UsingDeclarations { get; set; } = new HashSet<string>();
    }
}
