using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Mox.Extensions;
using Mox.Utils;

namespace Mox.Core
{
    internal static class StubGenerator
    {
        private static readonly MethodInfo GetMethodFromHandle_MethodInfo = typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle) });

        private static readonly MethodInfo GetTypeFromHandle_MethodInfo = typeof(Type).GetMethod("GetTypeFromHandle");

        private static readonly MethodInfo GetUninitializedObject_MethodInfo =
            typeof(RuntimeHelpers).GetMethod("GetUninitializedObject") == null ?
            typeof(FormatterServices).GetMethod("GetUninitializedObject") :
            typeof(RuntimeHelpers).GetMethod("GetUninitializedObject");

        /// <summary>
        /// 
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static DynamicMethod GenerateStubForDirectCall(Isolate isolate, MethodBase method)
        {
            if (isolate.StubCache.ContainsKey(method.MetadataToken))
            {
                return isolate.StubCache[method.MetadataToken];
            }
#if DEBUG
            Debug.WriteLine("    >>>> Generate stub for method:" + method);
#endif
            var signatureParamTypes = new List<Type>();
            if (!method.IsStatic)
            {
                var thisType = method.DeclaringType;
                if (thisType.IsValueType)
                {
                    thisType = thisType.MakeByRefType();
                }

                signatureParamTypes.Add(thisType);
            }
            signatureParamTypes.AddRange(method.GetParameters().Select(p => p.ParameterType));
            var returnType = method.IsConstructor ? typeof(void) : (method as MethodInfo).ReturnType;

            var stub = new DynamicMethod(
                StubHelper.CreateStubNameForMethod("stub_call", method),
                returnType,
                signatureParamTypes.ToArray(),
                StubHelper.GetOwningModule(),
                true);

            var locals = new List<LocalVariableInfo>();
            var ilGenerator = stub.GetILGenerator();

            if (method.GetMethodBody() == null || StubHelper.IsIntrinsic(method))
            {
                // Method has no Body or is a compiler intrinsic
                for (var i = 0; i < signatureParamTypes.Count; i++)
                {
                    ilGenerator.Emit(OpCodes.Ldarg, i);
                }

                if (method.IsConstructor)
                {
                    ilGenerator.Emit(OpCodes.Call, (ConstructorInfo)method);
                }
                else
                {
                    ilGenerator.Emit(OpCodes.Call, (MethodInfo)method);
                }

                ilGenerator.Emit(OpCodes.Ret);

#if DEBUG
                DebugOutputStubBody(stub, method.IsConstructor ? new Type[] { } : method.GetGenericArguments(), locals, "    ");
#endif
                isolate.StubCache[method.MetadataToken] = stub;
                return stub;
            }

            locals.Add(ilGenerator.DeclareLocal(typeof(IntPtr)));

            var rewriteLabel = ilGenerator.DefineLabel();
            var returnLabel = ilGenerator.DefineLabel();

            // Inject method info into instruction stream
            if (method.IsConstructor)
            {
                ilGenerator.Emit(OpCodes.Ldtoken, (ConstructorInfo)method);
            }
            else
            {
                ilGenerator.Emit(OpCodes.Ldtoken, (MethodInfo)method);
            }

            // Push MethodInfo
            ilGenerator.Emit(OpCodes.Ldtoken, method.DeclaringType);
            ilGenerator.Emit(OpCodes.Call, GetMethodFromHandle_MethodInfo);

            // Rewrite method
            ilGenerator.MarkLabel(rewriteLabel);
            if (signatureParamTypes.Count > 0)
            {
                ilGenerator.Emit(OpCodes.Ldarg_0);
            }
            else
            {
                ilGenerator.Emit(OpCodes.Ldnull);
            }
            ilGenerator.Emit(OpCodes.Ldc_I8, Isolate.ToInt64(isolate));
            ilGenerator.Emit(OpCodes.Ldc_I4_0);
            ilGenerator.Emit(OpCodes.Call, MethodRewriter.CreateRewriter_MethodInfo);
            ilGenerator.Emit(OpCodes.Call, MethodRewriter.Rewrite_MethodInfo);

            // Retrieve pointer to rewritten method
            ilGenerator.Emit(OpCodes.Call, StubHelper.GetMethodPointer_MethodInfo);
            ilGenerator.Emit(OpCodes.Stloc_0);

            // Setup stack and make indirect call
            for (var i = 0; i < signatureParamTypes.Count; i++)
            {
                ilGenerator.Emit(OpCodes.Ldarg, i);
            }
            ilGenerator.Emit(OpCodes.Ldloc_0);
            ilGenerator.EmitCalli(OpCodes.Calli, CallingConventions.Standard, returnType, signatureParamTypes.ToArray(), null);

            ilGenerator.MarkLabel(returnLabel);
            ilGenerator.Emit(OpCodes.Ret);

#if DEBUG
            DebugOutputStubBody(stub, method.IsConstructor ? new Type[] { } : method.GetGenericArguments(), locals, "    ");
#endif
            isolate.StubCache[method.MetadataToken] = stub;
            return stub;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="method"></param>
        /// <param name="constrainedType"></param>
        /// <returns></returns>
        public static DynamicMethod GenerateStubForVirtualCall(Isolate isolate, MethodInfo method, TypeInfo constrainedType)
        {
            if (isolate.StubCache.ContainsKey(method.MetadataToken))
            {
                return isolate.StubCache[method.MetadataToken];
            }
#if DEBUG
            Debug.WriteLine("    >>>>>> Generate stub for method:" + method);
#endif
            var thisType = constrainedType.MakeByRefType();
            var actualMethod = StubHelper.DevirtualizeMethod(constrainedType, method);
            var signatureParamTypes = new List<Type>
            {
                thisType
            };
            signatureParamTypes.AddRange(method.GetParameters().Select(p => p.ParameterType));

            var stub = new DynamicMethod(
                StubHelper.CreateStubNameForMethod("stub_callvirt", method),
                method.ReturnType,
                signatureParamTypes.ToArray(),
                StubHelper.GetOwningModule(),
                true);

            var locals = new List<LocalVariableInfo>();
            var ilGenerator = stub.GetILGenerator();

            if (actualMethod.GetMethodBody() == null && !actualMethod.IsAbstract || StubHelper.IsIntrinsic(actualMethod))
            {
                // Method has no Body or is a compiler intrinsic
                for (var i = 0; i < signatureParamTypes.Count; i++)
                {
                    ilGenerator.Emit(OpCodes.Ldarg, i);
                }

                ilGenerator.Emit(OpCodes.Call, actualMethod);
                ilGenerator.Emit(OpCodes.Ret);

#if DEBUG
                DebugOutputStubBody(stub, method.IsConstructor ? new Type[] { } : method.GetGenericArguments(), locals, "    ");
#endif
                isolate.StubCache[method.MetadataToken] = stub;
                return stub;
            }

            locals.Add(ilGenerator.DeclareLocal(typeof(IntPtr)));

            var rewriteLabel = ilGenerator.DefineLabel();
            var returnLabel = ilGenerator.DefineLabel();

            // Inject method info into instruction stream
            ilGenerator.Emit(OpCodes.Ldtoken, actualMethod);
            ilGenerator.Emit(OpCodes.Ldtoken, actualMethod.DeclaringType);
            ilGenerator.Emit(OpCodes.Call, GetMethodFromHandle_MethodInfo);
            ilGenerator.Emit(OpCodes.Castclass, typeof(MethodInfo));

            // Rewrite method
            ilGenerator.MarkLabel(rewriteLabel);
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldc_I8, Isolate.ToInt64(isolate));
            ilGenerator.Emit(OpCodes.Ldc_I4_0);
            ilGenerator.Emit(OpCodes.Call, MethodRewriter.CreateRewriter_MethodInfo);
            ilGenerator.Emit(OpCodes.Call, MethodRewriter.Rewrite_MethodInfo);
            ilGenerator.Emit(OpCodes.Castclass, typeof(MethodInfo));

            // Retrieve pointer to rewritten method
            ilGenerator.Emit(OpCodes.Call, StubHelper.GetMethodPointer_MethodInfo);
            ilGenerator.Emit(OpCodes.Stloc_0);

            // Setup stack and make indirect call
            for (var i = 0; i < signatureParamTypes.Count; i++)
            {
                ilGenerator.Emit(OpCodes.Ldarg, i);
                if (i == 0)
                {
                    if (!constrainedType.IsValueType)
                    {
                        ilGenerator.Emit(OpCodes.Ldind_Ref);
                        signatureParamTypes[i] = constrainedType;
                    }
                    else
                    {
                        if (actualMethod.DeclaringType != constrainedType)
                        {
                            ilGenerator.Emit(OpCodes.Ldobj, constrainedType);
                            ilGenerator.Emit(OpCodes.Box, constrainedType);
                            signatureParamTypes[i] = actualMethod.DeclaringType;
                        }
                    }
                }
            }
            ilGenerator.Emit(OpCodes.Ldloc_0);
            ilGenerator.EmitCalli(OpCodes.Calli, CallingConventions.Standard, method.ReturnType, signatureParamTypes.ToArray(), null);

            ilGenerator.MarkLabel(returnLabel);
            ilGenerator.Emit(OpCodes.Ret);

#if DEBUG
            DebugOutputStubBody(stub, method.GetGenericArguments(), locals, "    ");
#endif
            isolate.StubCache[method.MetadataToken] = stub;
            return stub;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static DynamicMethod GenerateStubForVirtualCall(Isolate isolate, MethodInfo method)
        {
            if (isolate.StubCache.ContainsKey(method.MetadataToken))
            {
                return isolate.StubCache[method.MetadataToken];
            }
#if DEBUG
            Debug.WriteLine("    >>>>>> Generate stub for method:" + method);
#endif
            var thisType = method.DeclaringType.IsInterface ? typeof(object) : method.DeclaringType;
            var signatureParamTypes = new List<Type>
            {
                thisType
            };
            signatureParamTypes.AddRange(method.GetParameters().Select(p => p.ParameterType));

            var stub = new DynamicMethod(
                StubHelper.CreateStubNameForMethod("stub_callvirt", method),
                method.ReturnType,
                signatureParamTypes.ToArray(),
                StubHelper.GetOwningModule(),
                true);

            var locals = new List<LocalVariableInfo>();
            var ilGenerator = stub.GetILGenerator();

            if (method.GetMethodBody() == null && !method.IsAbstract || StubHelper.IsIntrinsic(method))
            {
                // Method has no Body or is a compiler intrinsic
                for (var i = 0; i < signatureParamTypes.Count; i++)
                {
                    ilGenerator.Emit(OpCodes.Ldarg, i);
                }

                ilGenerator.Emit(OpCodes.Callvirt, method);
                ilGenerator.Emit(OpCodes.Ret);
#if DEBUG
                DebugOutputStubBody(stub, method.IsConstructor ? new Type[] { } : method.GetGenericArguments(), locals, "    ");
#endif
                isolate.StubCache[method.MetadataToken] = stub;
                return stub;
            }

            locals.Add(ilGenerator.DeclareLocal(typeof(MethodInfo)));
            locals.Add(ilGenerator.DeclareLocal(typeof(IntPtr)));

            var rewriteLabel = ilGenerator.DefineLabel();
            var returnLabel = ilGenerator.DefineLabel();

            // Inject method info into instruction stream
            ilGenerator.Emit(OpCodes.Ldtoken, method);
            ilGenerator.Emit(OpCodes.Ldtoken, method.DeclaringType);
            ilGenerator.Emit(OpCodes.Call, GetMethodFromHandle_MethodInfo);
            ilGenerator.Emit(OpCodes.Castclass, typeof(MethodInfo));
            ilGenerator.Emit(OpCodes.Stloc_0); // virtualMethod

            // Resolve virtual method to object type
            ilGenerator.Emit(OpCodes.Ldarg_0); // object
            ilGenerator.Emit(OpCodes.Ldloc_0); // virtualMethod
            ilGenerator.Emit(OpCodes.Call, StubHelper.DevirtualizeMethod_MethodInfo);

            // Rewrite resolved method
            ilGenerator.MarkLabel(rewriteLabel);
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldc_I8, Isolate.ToInt64(isolate));
            ilGenerator.Emit(method.DeclaringType.IsInterface ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            ilGenerator.Emit(OpCodes.Call, MethodRewriter.CreateRewriter_MethodInfo);
            ilGenerator.Emit(OpCodes.Call, MethodRewriter.Rewrite_MethodInfo);
            ilGenerator.Emit(OpCodes.Castclass, typeof(MethodInfo));

            // Retrieve pointer to rewritten method
            ilGenerator.Emit(OpCodes.Call, StubHelper.GetMethodPointer_MethodInfo);
            ilGenerator.Emit(OpCodes.Stloc_1);

            // Setup stack and make indirect call
            for (var i = 0; i < signatureParamTypes.Count; i++)
            {
                ilGenerator.Emit(OpCodes.Ldarg, i);
            }
            ilGenerator.Emit(OpCodes.Ldloc_1);
            ilGenerator.EmitCalli(OpCodes.Calli, CallingConventions.Standard, method.ReturnType, signatureParamTypes.ToArray(), null);

            ilGenerator.MarkLabel(returnLabel);
            ilGenerator.Emit(OpCodes.Ret);

#if DEBUG
            DebugOutputStubBody(stub, method.GetGenericArguments(), locals, "    ");
#endif
            isolate.StubCache[method.MetadataToken] = stub;
            return stub;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="constructor"></param>
        /// <returns></returns>
        public static DynamicMethod GenerateStubForObjectInitialization(Isolate isolate, ConstructorInfo constructor)
        {
            // check cache
            if (isolate.StubCache.ContainsKey(constructor.MetadataToken))
            {
                return isolate.StubCache[constructor.MetadataToken];
            }
#if DEBUG
            Debug.WriteLine("    >>>>>> Generate stub for constructor:" + constructor.Name);
#endif
            var thisType = constructor.DeclaringType;
            if (thisType.IsValueType)
            {
                thisType = thisType.MakeByRefType();
            }
            var signatureParamTypes = new List<Type>
            {
                thisType
            };
            signatureParamTypes.AddRange(constructor.GetParameters().Select(p => p.ParameterType));

            var stub = new DynamicMethod(
                StubHelper.CreateStubNameForMethod("stub_newobj", constructor),
                constructor.DeclaringType,
                signatureParamTypes.Skip(1).ToArray(),
                StubHelper.GetOwningModule(),
                true);

            var locals = new List<LocalVariableInfo>();
            var ilGenerator = stub.GetILGenerator();

            if (constructor.GetMethodBody() == null || StubHelper.IsIntrinsic(constructor))
            {
                // Constructor has no Body or is a compiler intrinsic
                for (var i = 0; i < signatureParamTypes.Count - 1; i++)
                {
                    ilGenerator.Emit(OpCodes.Ldarg, i);
                }

                ilGenerator.Emit(OpCodes.Newobj, constructor);
                ilGenerator.Emit(OpCodes.Ret);
#if DEBUG
                DebugOutputStubBody(stub, new Type[] { }, locals, "    ");
#endif
                isolate.StubCache[constructor.MetadataToken] = stub;
                return stub;
            }

            locals.Add(ilGenerator.DeclareLocal(typeof(IntPtr)));
            locals.Add(ilGenerator.DeclareLocal(constructor.DeclaringType));

            var rewriteLabel = ilGenerator.DefineLabel();
            var returnLabel = ilGenerator.DefineLabel();

            // Inject method info into instruction stream
            ilGenerator.Emit(OpCodes.Ldtoken, constructor);
            ilGenerator.Emit(OpCodes.Ldtoken, constructor.DeclaringType);
            ilGenerator.Emit(OpCodes.Call, GetMethodFromHandle_MethodInfo);

            // Rewrite method: CreateRewriter(method, obj, isInterface)
            ilGenerator.MarkLabel(rewriteLabel);
            ilGenerator.Emit(OpCodes.Ldnull);
            ilGenerator.Emit(OpCodes.Ldc_I8, Isolate.ToInt64(isolate));
            ilGenerator.Emit(OpCodes.Ldc_I4_0);
            ilGenerator.Emit(OpCodes.Call, MethodRewriter.CreateRewriter_MethodInfo);
            ilGenerator.Emit(OpCodes.Call, MethodRewriter.Rewrite_MethodInfo);

            // Retrieve pointer to rewritten constructor
            ilGenerator.Emit(OpCodes.Call, StubHelper.GetMethodPointer_MethodInfo);
            ilGenerator.Emit(OpCodes.Stloc_0);
            // rewritten constructor in local.0

            if (constructor.DeclaringType.IsValueType)
            {
                ilGenerator.Emit(OpCodes.Ldloca_S, 1);
                ilGenerator.Emit(OpCodes.Dup);
                ilGenerator.Emit(OpCodes.Initobj, constructor.DeclaringType);
                // new object on the stack top and local.1
            }
            else
            {
                ilGenerator.Emit(OpCodes.Ldtoken, constructor.DeclaringType);
                ilGenerator.Emit(OpCodes.Call, GetTypeFromHandle_MethodInfo);
                ilGenerator.Emit(OpCodes.Call, GetUninitializedObject_MethodInfo);
                ilGenerator.Emit(OpCodes.Dup);
                ilGenerator.Emit(OpCodes.Stloc_1);
                // new object on the stack top and local.1
            }

            // push all arguments
            for (var i = 0; i < signatureParamTypes.Count - 1; i++)
            {
                ilGenerator.Emit(OpCodes.Ldarg, i);
            }
            // push constructor
            ilGenerator.Emit(OpCodes.Ldloc_0);
            ilGenerator.EmitCalli(OpCodes.Calli, CallingConventions.Standard, typeof(void), signatureParamTypes.ToArray(), null);

            ilGenerator.MarkLabel(returnLabel);
            ilGenerator.Emit(OpCodes.Ldloc_1);
            // new object on the stack top
            ilGenerator.Emit(OpCodes.Ret);

#if DEBUG
            DebugOutputStubBody(stub, new Type[] { }, locals, "    ");
#endif
            isolate.StubCache[constructor.MetadataToken] = stub;
            return stub;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static DynamicMethod GenerateStubForDirectLoad(Isolate isolate, MethodBase method)
        {
            if (isolate.StubCache.ContainsKey(method.MetadataToken))
            {
                return isolate.StubCache[method.MetadataToken];
            }
#if DEBUG
            Debug.WriteLine("    >>>>>> Generate stub for method:" + method);
#endif
            var stub = new DynamicMethod(
                StubHelper.CreateStubNameForMethod("stub_ldftn", method),
                typeof(IntPtr),
                new Type[] { },
                StubHelper.GetOwningModule(),
                true);

            var locals = new List<LocalVariableInfo>();
            var ilGenerator = stub.GetILGenerator();

            if (method.GetMethodBody() == null || StubHelper.IsIntrinsic(method))
            {
                // Method has no Body or is a compiler intrinsic
                if (method.IsConstructor)
                {
                    ilGenerator.Emit(OpCodes.Ldftn, (ConstructorInfo)method);
                }
                else
                {
                    ilGenerator.Emit(OpCodes.Ldftn, (MethodInfo)method);
                }

                ilGenerator.Emit(OpCodes.Ret);

#if DEBUG
                DebugOutputStubBody(stub, method.IsConstructor ? new Type[] { } : method.GetGenericArguments(), locals, "    ");
#endif
                isolate.StubCache[method.MetadataToken] = stub;
                return stub;
            }

            var rewriteLabel = ilGenerator.DefineLabel();
            var returnLabel = ilGenerator.DefineLabel();

            // Inject method info into instruction stream
            if (method.IsConstructor)
            {
                ilGenerator.Emit(OpCodes.Ldtoken, (ConstructorInfo)method);
            }
            else
            {
                ilGenerator.Emit(OpCodes.Ldtoken, (MethodInfo)method);
            }

            ilGenerator.Emit(OpCodes.Ldtoken, method.DeclaringType);
            ilGenerator.Emit(OpCodes.Call, GetMethodFromHandle_MethodInfo);

            // Rewrite method
            ilGenerator.MarkLabel(rewriteLabel);
            ilGenerator.Emit(OpCodes.Ldnull);
            ilGenerator.Emit(OpCodes.Ldc_I8, Isolate.ToInt64(isolate));
            ilGenerator.Emit(OpCodes.Ldc_I4_0);
            ilGenerator.Emit(OpCodes.Call, MethodRewriter.CreateRewriter_MethodInfo);
            ilGenerator.Emit(OpCodes.Call, MethodRewriter.Rewrite_MethodInfo);

            // Retrieve pointer to rewritten method
            ilGenerator.Emit(OpCodes.Call, StubHelper.GetMethodPointer_MethodInfo);

            ilGenerator.MarkLabel(returnLabel);
            ilGenerator.Emit(OpCodes.Ret);

#if DEBUG
            DebugOutputStubBody(stub, method.GetGenericArguments(), locals, "    ");
#endif
            isolate.StubCache[method.MetadataToken] = stub;
            return stub;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static DynamicMethod GenerateStubForVirtualLoad(Isolate isolate, MethodInfo method)
        {
            if (isolate.StubCache.ContainsKey(method.MetadataToken))
            {
                return isolate.StubCache[method.MetadataToken];
            }
#if DEBUG
            Debug.WriteLine("    >>>>>> Generate stub for method:" + method);
#endif
            var stub = new DynamicMethod(
                StubHelper.CreateStubNameForMethod("stub_ldvirtftn", method),
                typeof(IntPtr),
                new Type[] { method.DeclaringType.IsInterface ? typeof(object) : method.DeclaringType },
                StubHelper.GetOwningModule(),
                true);

            var locals = new List<LocalVariableInfo>();
            var ilGenerator = stub.GetILGenerator();

            if (method.GetMethodBody() == null && !method.IsAbstract || StubHelper.IsIntrinsic(method))
            {
                // Method has no Body or is a compiler intrinsic
                ilGenerator.Emit(OpCodes.Ldarg_0);
                ilGenerator.Emit(OpCodes.Ldvirtftn, method);
                ilGenerator.Emit(OpCodes.Ret);

#if DEBUG
                DebugOutputStubBody(stub, method.IsConstructor ? new Type[] { } : method.GetGenericArguments(), locals, "    ");
#endif
                isolate.StubCache[method.MetadataToken] = stub;
                return stub;
            }

            locals.Add(ilGenerator.DeclareLocal(typeof(MethodInfo)));

            var rewriteLabel = ilGenerator.DefineLabel();
            var returnLabel = ilGenerator.DefineLabel();

            // Inject method info into instruction stream
            ilGenerator.Emit(OpCodes.Ldtoken, method);
            ilGenerator.Emit(OpCodes.Ldtoken, method.DeclaringType);
            ilGenerator.Emit(OpCodes.Call, GetMethodFromHandle_MethodInfo);
            ilGenerator.Emit(OpCodes.Castclass, typeof(MethodInfo));
            ilGenerator.Emit(OpCodes.Stloc_0);

            // Resolve virtual method to object type
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldloc_0);
            ilGenerator.Emit(OpCodes.Call, StubHelper.DevirtualizeMethod_MethodInfo);

            // Rewrite resolved method
            ilGenerator.MarkLabel(rewriteLabel);
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldc_I8, Isolate.ToInt64(isolate));
            ilGenerator.Emit(method.DeclaringType.IsInterface ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            ilGenerator.Emit(OpCodes.Call, MethodRewriter.CreateRewriter_MethodInfo);
            ilGenerator.Emit(OpCodes.Call, MethodRewriter.Rewrite_MethodInfo);

            // Retrieve pointer to rewritten method
            ilGenerator.Emit(OpCodes.Call, StubHelper.GetMethodPointer_MethodInfo);

            ilGenerator.MarkLabel(returnLabel);
            ilGenerator.Emit(OpCodes.Ret);

#if DEBUG
            DebugOutputStubBody(stub, method.GetGenericArguments(), locals, "    ");
#endif
            isolate.StubCache[method.MetadataToken] = stub;
            return stub;
        }

#if DEBUG
        static void DebugOutputStubBody(DynamicMethod stub, Type[] args, IList<LocalVariableInfo> locals, string prefix = "")
        {
            Debug.WriteLine($"{prefix}+++ ${stub}");
            foreach (var instruction in stub.GetILInstructions(args, locals))
            {
                Debug.WriteLine($"{prefix}    ${instruction}");
            }
            Debug.WriteLine($"{prefix}--- ${stub}");
        }
#endif
    }
}
