using Mox.Utils;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Mox
{
    /// <summary>
    /// 
    /// </summary>
    public partial class Isolate
    {
        /// <summary>
        /// 
        /// </summary>
        internal GCHandle _gcHandle;

        /// <summary>
        /// 
        /// </summary>
        internal List<Detour> DetourList = new List<Detour>();

        /// <summary>
        /// 
        /// </summary>
        internal Dictionary<int, DynamicMethod> StubCache { private set; get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        internal static Int64 ToInt64(Isolate self)
        {
            return GCHandle.ToIntPtr(self._gcHandle).ToInt64();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ptr"></param>
        /// <returns></returns>
        internal static Isolate FromInt64(Int64 ptr)
        {
            return (GCHandle.FromIntPtr((IntPtr)ptr).Target as WeakReference).Target as Isolate;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expression"></param>
        /// <param name="setter"></param>
        /// <returns></returns>
        private Detour WhenCalledImpl<T>(Expression<T> expression, bool setter)
        {
            var methodBase = MethodHelper.ExtracMethodFromExpression(expression.Body, setter, out var target);
            var detour = new Detour(this, methodBase, target);
            DetourList.Add(detour);
            return detour;
        }
    }
}
