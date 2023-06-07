using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;


namespace Mox.Utils
{
    internal static class MethodHelper
    {
        public static MethodInfo GetMethodByNameAndParameterTypeList(Type type, string name, Type[] paramTypes)
        {
            return type.GetMethod(name,
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                CallingConventions.Any,
                paramTypes,
                null);
        }

        public static MethodBase ExtracMethodFromExpression(Expression expression, bool setter, out object target)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.MemberAccess:
                    {
                        var memberExpression = expression as MemberExpression;
                        var memberInfo = memberExpression.Member;
                        if (memberInfo.MemberType == MemberTypes.Property)
                        {
                            var propertyInfo = memberInfo as PropertyInfo;
                            target = ExtractTargetFromExpression(memberExpression.Expression);
                            return setter ? propertyInfo.GetSetMethod() : propertyInfo.GetGetMethod();
                        }
                        else
                        {
                            throw new NotImplementedException("Unsupported expression");
                        }
                    }
                case ExpressionType.Call:
                    var methodCallExpression = expression as MethodCallExpression;
                    target = ExtractTargetFromExpression(methodCallExpression.Object);
                    return methodCallExpression.Method;
                case ExpressionType.New:
                    var newExpression = expression as NewExpression;
                    target = null;
                    return newExpression.Constructor;
                default:
                    break;
            }
            throw new NotImplementedException("Unsupported expression");
        }

        public static object ExtractTargetFromExpression(Expression expression)
        {
            object target = null;
            switch (expression?.NodeType)
            {
                case ExpressionType.MemberAccess:
                    {
                        var memberExpression = expression as MemberExpression;
                        var constantExpression = memberExpression.Expression as ConstantExpression;
                        if (memberExpression.Member.MemberType == MemberTypes.Field)
                        {
                            var fieldInfo = memberExpression.Member as FieldInfo;
                            var obj = fieldInfo.IsStatic ? null : constantExpression.Value;
                            target = fieldInfo.GetValue(obj);
                        }
                        else if (memberExpression.Member.MemberType == MemberTypes.Property)
                        {
                            var propertyInfo = memberExpression.Member as PropertyInfo;
                            var obj = propertyInfo.GetMethod.IsStatic ? null : constantExpression.Value;
                            target = propertyInfo.GetValue(obj);
                        }
                        if (target.GetType().IsSubclassOf(typeof(ValueType)))
                        {
                            throw new NotSupportedException("Methods replacing on ValueType instances is not supported");
                        }
                        break;
                    }
                case ExpressionType.Call:
                    {
                        var methodCallExpression = expression as MethodCallExpression;
                        var methodInfo = methodCallExpression.Method;
                        target = methodInfo.GetGenericArguments().FirstOrDefault();
                        break;
                    }
                default:
                    break;
            }

            return target;
        }

        public static void ValidateMethodSignature(MethodBase original, MethodInfo replacement)
        {
            var isValueType = original.DeclaringType.IsValueType;
            var isStatic = original.IsStatic;
            var isConstructor = original.IsConstructor;
            var isStaticOrConstructor = isStatic || isConstructor;

            var originalReturnType = isConstructor ? original.DeclaringType : (original as MethodInfo).ReturnType;
            originalReturnType = original.IsSpecialName ? typeof(void) : originalReturnType;
            var replaceReturnType = replacement.ReturnType;

            var orignalOwningType = original.DeclaringType;
            var replaceOwningType = isStaticOrConstructor
                ? orignalOwningType : replacement.GetParameters().Select(p => p.ParameterType).FirstOrDefault();

            var originalParameterTypes = original.GetParameters().Select(p => p.ParameterType);
            var reolaceParameterTypes = replacement.GetParameters()
                                        .Select(p => p.ParameterType)
                                        .Skip(isStaticOrConstructor ? 0 : 1);

            if (originalReturnType != replaceReturnType)
            {
                throw new ArgumentException("Mismatched return types");
            }

            if (!isStaticOrConstructor)
            {
                if (isValueType && !replaceOwningType.IsByRef)
                {
                    throw new ArgumentException("ValueType instances must be passed by ref");
                }
            }

            if ((isValueType && !isStaticOrConstructor ? orignalOwningType.MakeByRefType() : orignalOwningType) != replaceOwningType)
            {
                throw new ArgumentException("Mismatched instance types");
            }

            if (originalParameterTypes.Count() != reolaceParameterTypes.Count())
            {
                throw new ArgumentException("Parameters count do not match");
            }

            for (var i = 0; i < originalParameterTypes.Count(); i++)
            {
                if (originalParameterTypes.ElementAt(i) != reolaceParameterTypes.ElementAt(i))
                {
                    throw new ArgumentException($"Parameter types at {i} do not match");
                }
            }
        }
    }
}
