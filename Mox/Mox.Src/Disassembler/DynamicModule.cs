using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace Mox.Disassembler
{
    internal class DynamicModule : Module
    {
        private static FieldInfo ScopeField;

        private readonly ILGenerator ILGenerator;

        public DynamicModule(ILGenerator ilGenerator)
        {
            ILGenerator = ilGenerator;

            if (ScopeField == null)
            {
                ScopeField = ILGenerator
                    .GetType()
                    .GetField("m_scope", BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }

        public override string ResolveString(int metadataToken)
        {
            var dynamicScope = ScopeField.GetValue(ILGenerator);
            Debug.Assert(dynamicScope != null);

            return dynamicScope.GetType()
                .GetMethod("GetString", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(dynamicScope, new object[] { metadataToken })
                as string;
        }

        public override MemberInfo ResolveMember(int metadataToken, Type[] genericTypeArguments, Type[] genericMethodArguments)
        {
            var dynamicScope = ScopeField.GetValue(ILGenerator);
            Debug.Assert(dynamicScope != null);

            var handle = dynamicScope
                .GetType()
                .GetMethod("get_Item", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(dynamicScope, new object[] { metadataToken });

            Debug.Assert(handle != null);

            if (handle is RuntimeTypeHandle typeHandle)
            {
                return Type.GetTypeFromHandle(typeHandle).GetTypeInfo();
            }

            if (handle is RuntimeMethodHandle methodHandle)
            {
                return MethodBase.GetMethodFromHandle(methodHandle);
            }

            if (handle.GetType().ToString() == "System.Reflection.Emit.GenericMethodInfo")
            {
                var methodHandleFieldInfo = handle.GetType().GetField("m_methodHandle", BindingFlags.Instance | BindingFlags.NonPublic);
                var typeHandleFieldInfo = handle.GetType().GetField("m_context", BindingFlags.Instance | BindingFlags.NonPublic);
                var runtimeMethodHandle = (RuntimeMethodHandle)methodHandleFieldInfo.GetValue(handle);
                var runtimeTypeHanlde = (RuntimeTypeHandle)typeHandleFieldInfo.GetValue(handle);
                return MethodBase.GetMethodFromHandle(runtimeMethodHandle, runtimeTypeHanlde);
            }

            if (handle is RuntimeFieldHandle fieldHandle)
            {
                return FieldInfo.GetFieldFromHandle(fieldHandle);
            }

            if (handle.GetType().ToString() == "System.Reflection.Emit.GenericFieldInfo")
            {
                var fieldHandleFieldInfo = handle.GetType().GetField("m_fieldHandle", BindingFlags.Instance | BindingFlags.NonPublic);
                var typeHandleFieldInfo = handle.GetType().GetField("m_context", BindingFlags.Instance | BindingFlags.NonPublic);
                return FieldInfo.GetFieldFromHandle((RuntimeFieldHandle)fieldHandleFieldInfo.GetValue(handle), (RuntimeTypeHandle)typeHandleFieldInfo.GetValue(handle));
            }

            if (handle is DynamicMethod dynamicMethod)
            {
                return dynamicMethod;
            }

            throw new NotSupportedException(handle.ToString());
        }

        public override byte[] ResolveSignature(int metadataToken)
        {
            var dynamicScope = ScopeField.GetValue(ILGenerator);
            Debug.Assert(dynamicScope != null);

            return dynamicScope
                .GetType()
                .GetMethod("ResolveSignature", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(dynamicScope, new object[] { metadataToken, 0 })
                as byte[];
        }
    }
}
