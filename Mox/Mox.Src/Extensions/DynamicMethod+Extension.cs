using Mox.Decompiler;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Mox.Extensions
{
    internal static class DynamicMethodExtensionMethods
    {
        private static readonly MethodInfo GetMethodDescriptor_MethodInfo = typeof(DynamicMethod).GetMethod("GetMethodDescriptor", BindingFlags.Instance | BindingFlags.NonPublic);

        public static List<ILInstruction> GetILInstructions(this DynamicMethod @this, Type[] args, IList<System.Reflection.LocalVariableInfo> locals)
        {
            if (null == @this)
            {
                return new List<ILInstruction>();
            }

            return ILParser.ParseDynamicMethod(@this, args, locals);
        }

        public static RuntimeMethodHandle GetMethodDescriptor(this DynamicMethod @this)
        {
            return (RuntimeMethodHandle)GetMethodDescriptor_MethodInfo.Invoke(@this, null);
        }
    }
}
