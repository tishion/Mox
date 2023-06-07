using Mox.Core;
using Mox.Utils;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Mox
{
    public delegate void ActionRef<T>(ref T arg);
    public delegate void ActionRef<T1, T2>(ref T1 arg1, T2 arg2);
    public delegate void ActionRef<T1, T2, T3>(ref T1 arg1, T2 arg2, T3 arg3);
    public delegate void ActionRef<T1, T2, T3, T4>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    public delegate void ActionRef<T1, T2, T3, T4, T5>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    public delegate void ActionRef<T1, T2, T3, T4, T5, T6>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
    public delegate void ActionRef<T1, T2, T3, T4, T5, T6, T7>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
    public delegate void ActionRef<T1, T2, T3, T4, T5, T6, T7, T8>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
    public delegate void ActionRef<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9);
    public delegate void ActionRef<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10);

    public delegate TResult FuncRef<T1, TResult>(ref T1 arg1);
    public delegate TResult FuncRef<T1, T2, TResult>(ref T1 arg1, T2 arg2);
    public delegate TResult FuncRef<T1, T2, T3, TResult>(ref T1 arg1, T2 arg2, T3 arg3);
    public delegate TResult FuncRef<T1, T2, T3, T4, TResult>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    public delegate TResult FuncRef<T1, T2, T3, T4, T5, TResult>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    public delegate TResult FuncRef<T1, T2, T3, T4, T5, T6, TResult>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
    public delegate TResult FuncRef<T1, T2, T3, T4, T5, T6, T7, TResult>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
    public delegate TResult FuncRef<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
    public delegate TResult FuncRef<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9);
    public delegate TResult FuncRef<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10);

    /// <summary>
    /// 
    /// </summary>
    public interface IReplacable
    {
        Isolate ReplaceWith(Delegate replacement);

        Isolate ReplaceWith(Action replacement);

        Isolate ReplaceWith<T>(Action<T> replacement);

        Isolate ReplaceWith<T>(ActionRef<T> replacement);

        Isolate ReplaceWith<T1, T2>(Action<T1, T2> replacement);

        Isolate ReplaceWith<T1, T2>(ActionRef<T1, T2> replacement);

        Isolate ReplaceWith<T1, T2, T3>(Action<T1, T2, T3> replacement);

        Isolate ReplaceWith<T1, T2, T3>(ActionRef<T1, T2, T3> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4>(Action<T1, T2, T3, T4> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4>(ActionRef<T1, T2, T3, T4> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5>(ActionRef<T1, T2, T3, T4, T5> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6>(ActionRef<T1, T2, T3, T4, T5, T6> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7>(ActionRef<T1, T2, T3, T4, T5, T6, T7> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7, T8>(ActionRef<T1, T2, T3, T4, T5, T6, T7, T8> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Action<T1, T2, T3, T4, T5, T6, T7, T8, T9> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ActionRef<T1, T2, T3, T4, T5, T6, T7, T8, T9> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(Action<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(ActionRef<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> replacement);

        Isolate ReplaceWith<TResult>(Func<TResult> replacement);

        Isolate ReplaceWith<T1, TResult>(Func<T1, TResult> replacement);

        Isolate ReplaceWith<T1, TResult>(FuncRef<T1, TResult> replacement);

        Isolate ReplaceWith<T1, T2, TResult>(Func<T1, T2, TResult> replacement);

        Isolate ReplaceWith<T1, T2, TResult>(FuncRef<T1, T2, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, TResult>(Func<T1, T2, T3, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, TResult>(FuncRef<T1, T2, T3, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, TResult>(Func<T1, T2, T3, T4, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, TResult>(FuncRef<T1, T2, T3, T4, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, TResult>(Func<T1, T2, T3, T4, T5, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, TResult>(FuncRef<T1, T2, T3, T4, T5, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, TResult>(Func<T1, T2, T3, T4, T5, T6, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, TResult>(FuncRef<T1, T2, T3, T4, T5, T6, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7, TResult>(Func<T1, T2, T3, T4, T5, T6, T7, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7, TResult>(FuncRef<T1, T2, T3, T4, T5, T6, T7, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(FuncRef<T1, T2, T3, T4, T5, T6, T7, T8, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult>(FuncRef<T1, T2, T3, T4, T5, T6, T7, T8, T9, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> replacement);

        Isolate ReplaceWith<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult>(FuncRef<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, TResult> replacement);
    }

    /// <summary>
    /// 
    /// </summary>
    public static class On
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Class<T>()
        {
            return default(T);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public partial class Isolate
    {
        /// <summary>
        /// 
        /// </summary>
        public Isolate() : base()
        {
            _gcHandle = GCHandle.Alloc(new WeakReference(this));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="target"></param>
        /// <param name="method"></param>
        /// <param name="paramList"></param>
        /// <returns></returns>
        public IReplacable WhenCalled(object target, string method, params Type[] paramList)
        {
            Type type = target is Type ? target as Type : target.GetType();
            MethodBase methodBase = MethodHelper.GetMethodByNameAndParameterTypeList(type, method, paramList);
            var detour = new Detour(this, methodBase, target);
            DetourList.Add(detour);
            return detour;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public IReplacable WhenCalled(Expression<Action> expression)
        {
            return WhenCalledImpl(expression, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        public IReplacable WhenCalled<T>(Expression<Func<T>> expression)
        {
            return WhenCalledImpl(expression, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entryPoint"></param>
        public void Run(Action entryPoint)
        {
            StubCache = new Dictionary<int, DynamicMethod>();

            MethodRewriter
                .CreateRewriter(entryPoint.Method, entryPoint.Target, ToInt64(this), false)
                .Rewrite()
                .CreateDelegate(typeof(Action<>).MakeGenericType(new[] { entryPoint.Target.GetType() }))
                .DynamicInvoke(new[] { entryPoint.Target });

            StubCache.Clear();
        }
    }
}
