using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using Mox.Extensions;

namespace Mox.Utils
{
    internal static class StubHelper
    {
        public static readonly MethodInfo GetMethodPointer_MethodInfo = typeof(StubHelper).GetMethod("GetMethodPointer", new Type[] { typeof(MethodInfo) });

        public static readonly MethodInfo DevirtualizeMethod_MethodInfo = typeof(StubHelper).GetMethod("DevirtualizeMethod", new Type[] { typeof(object), typeof(MethodInfo) });

        public static readonly MethodInfo GetDetourDelegateTarget_MethodInfo = typeof(StubHelper).GetMethod("GetDetourDelegateTarget", new Type[] { typeof(Int64), typeof(int) });

        public static readonly MethodInfo BreakPointSlot_MethodInfo = typeof(StubHelper).GetMethod("BreakPointSlot");

        public static IntPtr GetMethodPointer(MethodInfo method)
        {
#if DEBUG
            Debug.WriteLine(">>   GetMethodPointer of: " + method.Name);
#endif
            RuntimeMethodHandle methodHandle;
            if (method is DynamicMethod dynamicMethod)
            {
                methodHandle = dynamicMethod.GetMethodDescriptor();
            }
            else
            {
                methodHandle = method.MethodHandle;
            }

#if DEBUG
            Debug.WriteLine(">>   Preparing to invoke: " + method.Name + "\n");
#endif
            return methodHandle.GetFunctionPointer();
        }

        public static object GetDetourDelegateTarget(Int64 ptr, int index)
        {
            var isolate = Isolate.FromInt64(ptr);
            return isolate.DetourList[index].Replacement.Target;
        }

        public static MethodInfo GetDetourReplacementMethod(Isolate isolate, int index)
            => isolate.DetourList[index].Replacement.Method;

        public static int GetIndexOfMatchingDetour(Isolate isolate, MethodBase methodBase, object obj)
        {
            // find static method item
            if (methodBase.IsStatic || obj == null)
            {
                return isolate.DetourList.FindIndex(s => s.Method == methodBase);
            }

            // try to match for instance call
            var index = isolate.DetourList.FindIndex(s => ReferenceEquals(obj, s.Target) && s.Method == methodBase);
            if (index == -1)
            {
                // try to match for type call
                return isolate.DetourList.FindIndex(s => s.Target is Type && SignatureEquals(s, methodBase));
            }

            return index;
        }

        public static MethodInfo DevirtualizeMethod(object obj, MethodInfo virtualMethod)
            => DevirtualizeMethod(obj.GetType(), virtualMethod);

        public static MethodInfo DevirtualizeMethod(Type thisType, MethodInfo virtualMethod)
        {
            if (thisType == virtualMethod.DeclaringType)
            {
                return virtualMethod;
            }

            var bindingFlags = BindingFlags.Instance | (virtualMethod.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic);
            var types = virtualMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            return thisType.GetMethod(virtualMethod.Name, bindingFlags, null, types, null);
        }

        public static Module GetOwningModule() => typeof(StubHelper).Module;

        public static bool IsIntrinsic(MethodBase method) =>
            method.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.Runtime.CompilerServices.IntrinsicAttribute")
            || method.DeclaringType.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.Runtime.CompilerServices.IntrinsicAttribute")
            || method.DeclaringType.FullName.StartsWith("System.Runtime.Intrinsics");

        public static string CreateStubNameForMethod(string prefix, MethodBase method) =>
            prefix + "_" + GetFullMethodName(method);

        public static string GetFullMethodName(MethodBase method)
        {
            var name = method.DeclaringType.ToString();
            name += "_";
            name += method.Name;

            if (!method.IsConstructor)
            {
                var genericArguments = method.GetGenericArguments();
                if (genericArguments.Length > 0)
                {
                    name += "[";
                    name += string.Join(",", genericArguments.Select(g => g.Name));
                    name += "]";
                }
            }

            return name;
        }

        public static void BreakPointSlot()
        {
            Debug.WriteLine("BreakPointSlot Hit");
        }

        private static bool SignatureEquals(Detour detour, MethodBase method)
        {
            if (method.DeclaringType == detour.Method.DeclaringType)
            {
                return $"{detour.Method.DeclaringType}::{detour.Method}" == $"{method.DeclaringType}::{method}";
            }

            if (method.DeclaringType.IsSubclassOf(detour.Method.DeclaringType))
            {
                if (detour.Method.IsAbstract || !detour.Method.IsVirtual
                        || detour.Method.IsVirtual && !method.IsOverride())
                {
                    return $"{detour.Method}" == $"{method}";
                }
            }

            return false;
        }
    }
}
