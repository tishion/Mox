using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using Mox.Extensions;
using Mox.Decompiler;
using Mox.Utils;

namespace Mox.Core
{
    internal class MethodRewriter
    {
        public static readonly MethodInfo CreateRewriter_MethodInfo
            = typeof(MethodRewriter).GetMethod("CreateRewriter", new Type[] { typeof(MethodBase), typeof(object), typeof(Int64), typeof(bool) });

        public static readonly MethodInfo Rewrite_MethodInfo
            = typeof(MethodRewriter).GetMethod("Rewrite");

        private Isolate Isolate;

        private object Target;

        private MethodBase Method;

        private bool IsInterfaceDispatch;

        private int ExceptionBlockLevel;

        private TypeInfo ConstrainedType;

        private static readonly List<OpCode> IngoredOpCodes = new List<OpCode> { OpCodes.Endfilter, OpCodes.Endfinally };

        public static MethodRewriter CreateRewriter(MethodBase method, object target, Int64 ptr, bool isInterfaceDispatch)
        {
#if DEBUG
            Debug.WriteLine("+++ Creating Rewriter for method: " + method);
#endif
            return new MethodRewriter
            {
                Isolate = Isolate.FromInt64(ptr),
                Target = target,
                Method = method,
                IsInterfaceDispatch = isInterfaceDispatch
            };
        }

        public MethodInfo Rewrite()
        {
            DynamicMethod dynamicMethod = null;
            var index = StubHelper.GetIndexOfMatchingDetour(Isolate, Method, Target);
            if (index < 0)
            {
#if DEBUG
                Debug.WriteLine("+++ Rewriting method:" + Method);
#endif
                dynamicMethod = DoRewrite();
#if DEBUG
                Debug.WriteLine("--- Rewriting done\n");
#endif
            }
            else
            {
#if DEBUG
                Debug.WriteLine("+++ Redirecting to replacement for method: " + Method);
#endif
                dynamicMethod = DoRedirect(index);
#if DEBUG
                Debug.WriteLine("--- Redirecting done");
#endif
            }

            return dynamicMethod;
        }

        private DynamicMethod DoRewrite()
        {
            // collect original method information
            var parameterTypes = new List<Type>();
            if (!Method.IsStatic)
            {
                var thisType = IsInterfaceDispatch ? typeof(object) : Method.DeclaringType;
                if (!IsInterfaceDispatch && Method.DeclaringType.IsValueType)
                {
                    thisType = thisType.MakeByRefType();
                }

                parameterTypes.Add(thisType);
            }
            parameterTypes.AddRange(Method.GetParameters().Select(p => p.ParameterType));
            var returnType = Method.IsConstructor ? typeof(void) : (Method as MethodInfo).ReturnType;
            var methodBody = Method.GetMethodBody();
            var originalInstructions = Method.GetILInstructions();

#if DEBUG
            Debug.WriteLine("+++ Original:");
            DebugOutputMethodBody(Method, Method.GetILInstructions());
#endif

            // create dynamic method
            var dynamicMethod = new DynamicMethod(
                StubHelper.CreateStubNameForMethod("impl", Method),
                returnType,
                parameterTypes.ToArray(),
                StubHelper.GetOwningModule(),
                true);
            var locals = new List<LocalVariableInfo>();
            var targetInstructions = new Dictionary<long, Label>();
            var handlers = new List<ExceptionHandler>();
            var ilGenerator = dynamicMethod.GetILGenerator();

            foreach (var clause in methodBody.ExceptionHandlingClauses)
            {
                var handler = new ExceptionHandler
                {
                    Flags = clause.Flags,
                    CatchType = clause.Flags == ExceptionHandlingClauseOptions.Clause ? clause.CatchType : null,
                    TryStart = clause.TryOffset,
                    TryEnd = clause.TryOffset + clause.TryLength,
                    FilterStart = clause.Flags == ExceptionHandlingClauseOptions.Filter ? clause.FilterOffset : -1,
                    HandlerStart = clause.HandlerOffset,
                    HandlerEnd = clause.HandlerOffset + clause.HandlerLength
                };
                handlers.Add(handler);
            }

            foreach (var local in methodBody.LocalVariables)
            {
                locals.Add(ilGenerator.DeclareLocal(local.LocalType, local.IsPinned));
            }

            var ifTargets = originalInstructions
                .Where(i => i.Operand as ILInstruction != null)
                .Select(i => i.Operand as ILInstruction)
                .Distinct();
            foreach (var instruction in ifTargets)
            {
                targetInstructions.TryAdd(instruction.Offset, ilGenerator.DefineLabel());
            }

            var switchTargets = originalInstructions
                .Where(i => i.Operand as ILInstruction[] != null)
                .Select(i => i.Operand as ILInstruction[])
                .Distinct();
            foreach (var switchInstructions in switchTargets)
            {
                foreach (var instruction in switchInstructions)
                {
                    targetInstructions.TryAdd(instruction.Offset, ilGenerator.DefineLabel());
                }
            }

            foreach (var instruction in originalInstructions)
            {
                EmitILForExceptionHandlers(ilGenerator, instruction, handlers);

                if (targetInstructions.TryGetValue(instruction.Offset, out var label))
                {
                    ilGenerator.MarkLabel(label);
                }

                if (IngoredOpCodes.Contains(instruction.OpCode))
                {
                    continue;
                }

                switch (instruction.OpCode.OperandType)
                {
                    case OperandType.InlineNone:
                        EmitILForInlineNone(ilGenerator, instruction);
                        break;
                    case OperandType.InlineI:
                        EmitILForInlineI(ilGenerator, instruction);
                        break;
                    case OperandType.InlineI8:
                        EmitILForInlineI8(ilGenerator, instruction);
                        break;
                    case OperandType.ShortInlineI:
                        EmitILForShortInlineI(ilGenerator, instruction);
                        break;
                    case OperandType.InlineR:
                        EmitILForInlineR(ilGenerator, instruction);
                        break;
                    case OperandType.ShortInlineR:
                        EmitILForShortInlineR(ilGenerator, instruction);
                        break;
                    case OperandType.InlineString:
                        EmitILForInlineString(ilGenerator, instruction);
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.InlineBrTarget:
                        EmitILForInlineBrTarget(ilGenerator, instruction, targetInstructions);
                        break;
                    case OperandType.InlineSwitch:
                        EmitILForInlineSwitch(ilGenerator, instruction, targetInstructions);
                        break;
                    case OperandType.ShortInlineVar:
                    case OperandType.InlineVar:
                        EmitILForInlineVar(ilGenerator, instruction);
                        break;
                    case OperandType.InlineTok:
                    case OperandType.InlineType:
                    case OperandType.InlineField:
                    case OperandType.InlineMethod:
                        EmitILForInlineMember(ilGenerator, instruction);
                        break;
                    case OperandType.InlineSig:
                    default:
                        throw new NotSupportedException(instruction.OpCode.OperandType.ToString());
                }
            }

#if DEBUG
            Debug.WriteLine("+++ Rewritten:");
            var args = Method.IsConstructor ? new Type[] { } : Method.GetGenericArguments();
            var ilList = dynamicMethod.GetILInstructions(args, locals);
            DebugOutputMethodBody(dynamicMethod, ilList);
#endif

            return dynamicMethod;
        }

        private DynamicMethod DoRedirect(int index)
        {
            // collect original method information
            var parameterTypes = new List<Type>();
            if (!Method.IsStatic)
            {
                var thisType = IsInterfaceDispatch ? typeof(object) : Method.DeclaringType;
                if (!IsInterfaceDispatch && Method.DeclaringType.IsValueType)
                {
                    thisType = thisType.MakeByRefType();
                }

                parameterTypes.Add(thisType);
            }
            parameterTypes.AddRange(Method.GetParameters().Select(p => p.ParameterType));
            var returnType = Method.IsConstructor ? typeof(void) : (Method as MethodInfo).ReturnType;

#if DEBUG
            Debug.WriteLine("+++ Original:");
            DebugOutputMethodBody(Method, Method.GetILInstructions());
#endif

            // create dynamic method
            var dynamicMethod = new DynamicMethod(
                StubHelper.CreateStubNameForMethod("impl", Method),
                returnType,
                parameterTypes.ToArray(),
                StubHelper.GetOwningModule(),
                true);
            var locals = new List<LocalVariableInfo>();
            var ilGenerator = dynamicMethod.GetILGenerator();
            /* 
             * Generate the following code by Decompiler
             * 
             * var obj = StubHelper.GetDetourDelegateTarget(Int64 ptr, index);
             * var method = StubHelper.GetDetourReplacementMethod(index);
             * return method(obj, arg0, arg1, ...);
             * 
             */
            ilGenerator.Emit(OpCodes.Ldc_I8, Isolate.ToInt64(this.Isolate));
            ilGenerator.Emit(OpCodes.Ldc_I4, index);
            ilGenerator.Emit(OpCodes.Call, StubHelper.GetDetourDelegateTarget_MethodInfo);
            for (var i = 0; i < parameterTypes.Count; i++)
            {
                ilGenerator.Emit(OpCodes.Ldarg, i);
            }
            ilGenerator.Emit(OpCodes.Call, StubHelper.GetDetourReplacementMethod(Isolate, index));

            ilGenerator.Emit(OpCodes.Ret);

#if DEBUG
            Debug.WriteLine("+++ Rewritten:");
            var args = Method.IsConstructor ? new Type[] { } : Method.GetGenericArguments();
            var ilList = dynamicMethod.GetILInstructions(args, locals);
            DebugOutputMethodBody(dynamicMethod, ilList);
#endif

            return dynamicMethod;
        }

        private void EmitILForExceptionHandlers(ILGenerator ilGenerator, ILInstruction instruction, List<ExceptionHandler> handlers)
        {
            var tryBlocks = handlers.Where(h => h.TryStart == instruction.Offset).GroupBy(h => h.TryEnd);
            foreach (var tryBlock in tryBlocks)
            {
                ilGenerator.BeginExceptionBlock();
                ExceptionBlockLevel++;
            }

            var filterBlock = handlers.FirstOrDefault(h => h.FilterStart == instruction.Offset);
            if (filterBlock != null)
            {
                ilGenerator.BeginExceptFilterBlock();
            }

            var handler = handlers.FirstOrDefault(h => h.HandlerEnd == instruction.Offset);
            if (handler != null)
            {
                if (handler.Flags == ExceptionHandlingClauseOptions.Finally)
                {
                    // Finally blocks are always the last handler
                    ilGenerator.EndExceptionBlock();
                    ExceptionBlockLevel--;
                }
                else if (handler.HandlerEnd == handlers.Where(h => h.TryStart == handler.TryStart && h.TryEnd == handler.TryEnd).Max(h => h.HandlerEnd))
                {
                    // We're dealing with the last catch block
                    ilGenerator.EndExceptionBlock();
                    ExceptionBlockLevel--;
                }
            }

            var catchOrFinallyBlock = handlers.FirstOrDefault(h => h.HandlerStart == instruction.Offset);
            if (catchOrFinallyBlock != null)
            {
                if (catchOrFinallyBlock.Flags == ExceptionHandlingClauseOptions.Clause)
                {
                    ilGenerator.BeginCatchBlock(catchOrFinallyBlock.CatchType);
                }
                else if (catchOrFinallyBlock.Flags == ExceptionHandlingClauseOptions.Filter)
                {
                    ilGenerator.BeginCatchBlock(null);
                }
                else if (catchOrFinallyBlock.Flags == ExceptionHandlingClauseOptions.Finally)
                {
                    ilGenerator.BeginFinallyBlock();
                }
                else
                {
                    // No support for fault blocks
                    throw new NotSupportedException();
                }
            }
        }

        private void EmitThisPointerAccessForBoxedValueType(ILGenerator ilGenerator)
            => ilGenerator.Emit(OpCodes.Call, typeof(Unsafe).GetMethod("Unbox").MakeGenericMethod(Method.DeclaringType));

        private void EmitILForInlineNone(ILGenerator ilGenerator, ILInstruction instruction)
        {
            ilGenerator.Emit(instruction.OpCode);
            if (IsInterfaceDispatch && Method.DeclaringType.IsValueType && instruction.OpCode == OpCodes.Ldarg_0)
            {
                EmitThisPointerAccessForBoxedValueType(ilGenerator);
            }
        }

        private void EmitILForInlineI(ILGenerator ilGenerator, ILInstruction instruction)
            => ilGenerator.Emit(instruction.OpCode, (int)instruction.Operand);

        private void EmitILForInlineI8(ILGenerator ilGenerator, ILInstruction instruction)
            => ilGenerator.Emit(instruction.OpCode, (long)instruction.Operand);

        private void EmitILForShortInlineI(ILGenerator ilGenerator, ILInstruction instruction)
        {
            if (instruction.OpCode == OpCodes.Ldc_I4_S)
            {
                ilGenerator.Emit(instruction.OpCode, (sbyte)instruction.Operand);
            }
            else
            {
                ilGenerator.Emit(instruction.OpCode, (byte)instruction.Operand);
            }
        }

        private void EmitILForInlineR(ILGenerator ilGenerator, ILInstruction instruction)
            => ilGenerator.Emit(instruction.OpCode, (double)instruction.Operand);

        private void EmitILForShortInlineR(ILGenerator ilGenerator, ILInstruction instruction)
            => ilGenerator.Emit(instruction.OpCode, (float)instruction.Operand);

        private void EmitILForInlineString(ILGenerator ilGenerator, ILInstruction instruction)
            => ilGenerator.Emit(instruction.OpCode, (string)instruction.Operand);

        private void EmitILForInlineBrTarget(ILGenerator ilGenerator,
            ILInstruction instruction, Dictionary<long, Label> targetInstructions)
        {
            var targetLabel = targetInstructions[(instruction.Operand as ILInstruction).Offset];

            var opCode = instruction.OpCode;

            //// Offset values could change and not be short form anymore
            //if (opCode == OpCodes.Br_S)
            //{
            //    opCode = OpCodes.Br;
            //}
            //else if (opCode == OpCodes.Brfalse_S)
            //{
            //    opCode = OpCodes.Brfalse;
            //}
            //else if (opCode == OpCodes.Brtrue_S)
            //{
            //    opCode = OpCodes.Brtrue;
            //}
            //else if (opCode == OpCodes.Beq_S)
            //{
            //    opCode = OpCodes.Beq;
            //}
            //else if (opCode == OpCodes.Bge_S)
            //{
            //    opCode = OpCodes.Bge;
            //}
            //else if (opCode == OpCodes.Bgt_S)
            //{
            //    opCode = OpCodes.Bgt;
            //}
            //else if (opCode == OpCodes.Ble_S)
            //{
            //    opCode = OpCodes.Ble;
            //}
            //else if (opCode == OpCodes.Blt_S)
            //{
            //    opCode = OpCodes.Blt;
            //}
            //else if (opCode == OpCodes.Bne_Un_S)
            //{
            //    opCode = OpCodes.Bne_Un;
            //}
            //else if (opCode == OpCodes.Bge_Un_S)
            //{
            //    opCode = OpCodes.Bge_Un;
            //}
            //else if (opCode == OpCodes.Bgt_Un_S)
            //{
            //    opCode = OpCodes.Bgt_Un;
            //}
            //else if (opCode == OpCodes.Ble_Un_S)
            //{
            //    opCode = OpCodes.Ble_Un;
            //}
            //else if (opCode == OpCodes.Blt_Un_S)
            //{
            //    opCode = OpCodes.Blt_Un;
            //}
            //else if (opCode == OpCodes.Leave_S)
            //{
            //    opCode = OpCodes.Leave;
            //}

            // Check if 'Leave' opcode is being used in an exception block,
            // only emit it if that's not the case
            if ((opCode == OpCodes.Leave || opCode == OpCodes.Leave_S) && ExceptionBlockLevel > 0)
            {
                return;
            }

            ilGenerator.Emit(opCode, targetLabel);
        }

        private void EmitILForInlineSwitch(ILGenerator ilGenerator,
            ILInstruction instruction, Dictionary<long, Label> targetInstructions)
        {
            var switchInstructions = (ILInstruction[])instruction.Operand;
            var targetLabels = new Label[switchInstructions.Length];
            for (var i = 0; i < switchInstructions.Length; i++)
            {
                targetLabels[i] = targetInstructions[switchInstructions[i].Offset];
            }

            ilGenerator.Emit(instruction.OpCode, targetLabels);
        }

        private void EmitILForInlineVar(ILGenerator ilGenerator, ILInstruction instruction)
        {
            int index;
            if (instruction.OpCode.Name.Contains("loc"))
            {
                index = ((LocalVariableInfo)instruction.Operand).LocalIndex;
            }
            else
            {
                index = ((ParameterInfo)instruction.Operand).Position;
                index += Method.IsStatic ? 0 : 1;
            }

            if (instruction.OpCode.OperandType == OperandType.ShortInlineVar)
            {
                ilGenerator.Emit(instruction.OpCode, (byte)index);
            }
            else
            {
                ilGenerator.Emit(instruction.OpCode, (ushort)index);
            }

            if (IsInterfaceDispatch && Method.DeclaringType.IsValueType && instruction.OpCode.Name.StartsWith("ldarg") && index == 0)
            {
                EmitThisPointerAccessForBoxedValueType(ilGenerator);
            }
        }

        private void EmitILForType(ILGenerator ilGenerator, ILInstruction instruction, TypeInfo typeInfo)
        {
            if (instruction.OpCode == OpCodes.Constrained)
            {
                ConstrainedType = typeInfo;
                return;
            }

            ilGenerator.Emit(instruction.OpCode, typeInfo);
        }

        private void EmitILForConstructor(ILGenerator ilGenerator, ILInstruction instruction, ConstructorInfo constructorInfo)
        {
            if (constructorInfo.IsClrBuiltInAssembly())
            {
                if (!constructorInfo.DeclaringType.IsPublic)
                {
                    goto forward;
                }

                if (!constructorInfo.IsPublic && !constructorInfo.IsFamily && !constructorInfo.IsFamilyOrAssembly)
                {
                    goto forward;
                }
            }

            if (instruction.OpCode == OpCodes.Newobj)
            {
                ilGenerator.Emit(OpCodes.Call, StubGenerator.GenerateStubForObjectInitialization(Isolate, constructorInfo));
                return;
            }

            if (instruction.OpCode == OpCodes.Call)
            {
                ilGenerator.Emit(OpCodes.Call, StubGenerator.GenerateStubForDirectCall(Isolate, constructorInfo));
                return;
            }

            if (instruction.OpCode == OpCodes.Ldftn)
            {
                ilGenerator.Emit(OpCodes.Call, StubGenerator.GenerateStubForDirectLoad(Isolate, constructorInfo));
                return;
            }

            // If we get here, then we haven't accounted for an opcode.
            // Throw exception to make this obvious.
            throw new NotSupportedException(instruction.OpCode.Name);

        forward:
            ilGenerator.Emit(instruction.OpCode, constructorInfo);
        }

        private void EmitILForMethod(ILGenerator ilGenerator, ILInstruction instruction, MethodInfo methodInfo)
        {
            if (methodInfo.IsClrBuiltInAssembly())
            {
                if (!methodInfo.DeclaringType.IsPublic)
                {
                    goto forward;
                }

                if (!methodInfo.IsPublic && !methodInfo.IsFamily && !methodInfo.IsFamilyOrAssembly)
                {
                    goto forward;
                }
            }

            if (instruction.OpCode == OpCodes.Call)
            {
                ilGenerator.Emit(OpCodes.Call, StubGenerator.GenerateStubForDirectCall(Isolate, methodInfo));
                return;
            }

            if (instruction.OpCode == OpCodes.Callvirt)
            {
                if (ConstrainedType != null)
                {
                    ilGenerator.Emit(OpCodes.Call, StubGenerator.GenerateStubForVirtualCall(Isolate, methodInfo, ConstrainedType));
                    ConstrainedType = null;
                    return;
                }

                ilGenerator.Emit(OpCodes.Call, StubGenerator.GenerateStubForVirtualCall(Isolate, methodInfo));
                return;
            }

            if (instruction.OpCode == OpCodes.Ldftn)
            {
                ilGenerator.Emit(OpCodes.Call, StubGenerator.GenerateStubForDirectLoad(Isolate, methodInfo));
                return;
            }

            if (instruction.OpCode == OpCodes.Ldvirtftn)
            {
                ilGenerator.Emit(OpCodes.Call, StubGenerator.GenerateStubForVirtualLoad(Isolate, methodInfo));
                return;
            }

        forward:
            ilGenerator.Emit(instruction.OpCode, methodInfo);
        }

        private void EmitILForInlineMember(ILGenerator ilGenerator, ILInstruction instruction)
        {
            var memberInfo = (MemberInfo)instruction.Operand;
            if (memberInfo.MemberType == MemberTypes.Field)
            {
                ilGenerator.Emit(instruction.OpCode, memberInfo as FieldInfo);
            }
            else if (memberInfo.MemberType == MemberTypes.TypeInfo || memberInfo.MemberType == MemberTypes.NestedType)
            {
                EmitILForType(ilGenerator, instruction, memberInfo as TypeInfo);
            }
            else if (memberInfo.MemberType == MemberTypes.Constructor)
            {
                EmitILForConstructor(ilGenerator, instruction, memberInfo as ConstructorInfo);
            }
            else if (memberInfo.MemberType == MemberTypes.Method)
            {
                EmitILForMethod(ilGenerator, instruction, memberInfo as MethodInfo);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

#if DEBUG
        private void DebugOutputMethodBody(MethodBase m,  List<ILInstruction> ilList, string prefix = "")
        {
            Debug.WriteLine($"{prefix}+++ ${m}");
            foreach (var instruction in ilList)
            {
                Debug.WriteLine($"{prefix}    {instruction}");
            }
            Debug.WriteLine($"{prefix}--- ${m}");
        }
#endif
    }
}
