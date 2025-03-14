using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync
{
    public class DynamicPatchInfo
    {
        public Type DeclaringType { get; set; }

        public List<MethodInfo> TargetMethods { get; set; } = new List<MethodInfo>();

        public List<DynamicPatchMemberInfo> MemberInfos { get; set; } = new List<DynamicPatchMemberInfo>();
    }
}
