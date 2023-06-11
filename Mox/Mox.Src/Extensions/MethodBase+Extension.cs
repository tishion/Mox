using System;
using System.Collections.Generic;
using System.Reflection;

using Mox.Disassembler;

namespace Mox.Extensions
{
    internal static class MethodBaseExtensionMethods
    {
        public static bool IsClrBuiltInAssembly(this MethodBase @this)
        {
            if (null == @this)
            {
                return false;
            }

            return @this.DeclaringType.Assembly == typeof(Exception).Assembly;
        }

        public static bool IsOverride(this MethodBase @this)
        {
            if (!(@this is MethodInfo methodInfo))
            {
                return false;
            }

            return methodInfo.GetBaseDefinition().DeclaringType != methodInfo.DeclaringType;
        }

        public static IList<Instruction> GetILInstructions(this MethodBase @this)
        {
            if (null == @this)
            {
                return new List<Instruction>();
            }

            return @this.GetInstructions();
        }
    }
}
