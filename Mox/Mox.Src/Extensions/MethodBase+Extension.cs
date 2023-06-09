using System;
using System.Collections.Generic;
using System.Reflection;
using Mox.Decompiler;

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

        public static List<ILInstruction> GetILInstructions(this MethodBase @this)
        {
            if (null == @this)
            {
                return new List<ILInstruction>();
            }

            return ILParser.ParseMethod(@this);
        }
    }
}
