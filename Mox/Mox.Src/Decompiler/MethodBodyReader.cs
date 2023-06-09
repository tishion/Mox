using Mox.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace Mox.Decompiler
{
    internal class ILParser
    {
        public static List<ILInstruction> ParseMethod(MethodBase method)
        {
            var reader = new ILParser(method);
            reader.ReadInstructions();
            return reader.InstructionList;
        }

        public static List<ILInstruction> ParseDynamicMethod(DynamicMethod method, Type[] args, IList<LocalVariableInfo> locals)
        {
            var reader = new ILParser(method, args, locals);
            reader.ReadInstructions();
            return reader.InstructionList;
        }

        static readonly OpCode[] OneByteOpCodes;
        static readonly OpCode[] TwoBytesOpCodes;

        static ILParser()
        {
            OneByteOpCodes = new OpCode[0xe1];
            TwoBytesOpCodes = new OpCode[0x1f];

            var fields = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in fields)
            {
                var opcode = (OpCode)field.GetValue(null);
                if (opcode.OpCodeType == OpCodeType.Nternal)
                {
                    continue;
                }

                if (opcode.Size == 1)
                {
                    OneByteOpCodes[opcode.Value] = opcode;
                }
                else
                {
                    TwoBytesOpCodes[opcode.Value & 0xff] = opcode;
                }
            }
        }

        readonly MethodBase Method;
        readonly Module Module;
        readonly Type[] TypeArguments;
        readonly Type[] MethodArguments;
        readonly BinaryReader ILStreamReader;
        readonly ParameterInfo ThisInfo;
        readonly ParameterInfo[] Parameters;
        readonly IList<LocalVariableInfo> LocalVariables;
        readonly List<ILInstruction> InstructionList;

        ILParser(MethodBase method)
        {
            Method = method;

            var body = method.GetMethodBody();
            if (body == null)
            {
                throw new ArgumentException("Method has no Body");
            }

            var bytes = body.GetILAsByteArray();
            if (bytes == null)
            {
                throw new ArgumentException("Can not get Decompiler bytes data of the method");
            }

            if (!(method is ConstructorInfo))
            {
                MethodArguments = method.GetGenericArguments();
            }

            if (method.DeclaringType != null)
            {
                TypeArguments = method.DeclaringType.GetGenericArguments();
            }

            if (!method.IsStatic)
            {
                ThisInfo = new ThisParameter(method);
            }

            Parameters = method.GetParameters();
            LocalVariables = body.LocalVariables;
            Module = method.Module;
            ILStreamReader = new BinaryReader(new MemoryStream(bytes));
            InstructionList = new List<ILInstruction>((bytes.Length + 1) / 2);
        }

        ILParser(DynamicMethod method, Type[] paramTypes, IList<LocalVariableInfo> locals)
        {
            Method = method;

            var ilGenerator = method.GetILGenerator();
            if (ilGenerator == null)
            {
                throw new ArgumentException("Method has no ILGenerator");
            }

            var bytes = ilGenerator.GetILBytes();
            if (bytes == null)
            {
                throw new ArgumentException("Can not get Decompiler bytes data of the method");
            }

            if (method.DeclaringType != null)
            {
                TypeArguments = method.DeclaringType.GetGenericArguments();
            }

            if (!method.IsStatic)
            {
                ThisInfo = new ThisParameter(method);
            }

            //MethodArguments = method.GetGenericArguments();
            Parameters = method.GetParameters();
            LocalVariables = locals;
            MethodArguments = paramTypes;
            Module = new DynamicModule(ilGenerator);
            ILStreamReader = new BinaryReader(new MemoryStream(bytes));
            InstructionList = new List<ILInstruction>((bytes.Length + 1) / 2);
        }

        void ReadInstructions()
        {
            ILInstruction previous = null;

            while (ILStreamReader.BaseStream.Position < ILStreamReader.BaseStream.Length)
            {
                var instruction = new ILInstruction(ILStreamReader.BaseStream.Position, ReadOpCode());

                ReadOperand(instruction);

                if (previous != null)
                {
                    instruction.Previous = previous;
                    previous.Next = instruction;
                }

                InstructionList.Add(instruction);
                previous = instruction;
            }

            ResolveBranches();
        }

        void ReadOperand(ILInstruction instruction)
        {
            switch (instruction.OpCode.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.InlineSwitch:
                    int length = ILStreamReader.ReadInt32();
                    long base_offset = ILStreamReader.BaseStream.Position + 4 * length;
                    long[] branches = new long[length];
                    for (int i = 0; i < length; i++)
                    {
                        branches[i] = ILStreamReader.ReadInt32() + base_offset;
                    }

                    instruction.Operand = branches;
                    break;
                case OperandType.ShortInlineBrTarget:
                    instruction.Operand = (sbyte)ILStreamReader.ReadByte() + ILStreamReader.BaseStream.Position;
                    break;
                case OperandType.InlineBrTarget:
                    instruction.Operand = ILStreamReader.ReadInt32() + ILStreamReader.BaseStream.Position;
                    break;
                case OperandType.ShortInlineI:
                    if (instruction.OpCode == OpCodes.Ldc_I4_S)
                    {
                        instruction.Operand = (sbyte)ILStreamReader.ReadByte();
                    }
                    else
                    {
                        instruction.Operand = ILStreamReader.ReadByte();
                    }

                    break;
                case OperandType.InlineI:
                    instruction.Operand = ILStreamReader.ReadInt32();
                    break;
                case OperandType.ShortInlineR:
                    instruction.Operand = ILStreamReader.ReadSingle();
                    break;
                case OperandType.InlineR:
                    instruction.Operand = ILStreamReader.ReadDouble();
                    break;
                case OperandType.InlineI8:
                    instruction.Operand = ILStreamReader.ReadInt64();
                    break;
                case OperandType.InlineSig:
                    instruction.Operand = Module.ResolveSignature(ILStreamReader.ReadInt32());
                    break;
                case OperandType.InlineString:
                    instruction.Operand = Module.ResolveString(ILStreamReader.ReadInt32());
                    break;
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.InlineMethod:
                case OperandType.InlineField:
                    instruction.Operand = Module.ResolveMember(ILStreamReader.ReadInt32(), TypeArguments, MethodArguments);
                    break;
                case OperandType.ShortInlineVar:
                    instruction.Operand = GetVariable(instruction, ILStreamReader.ReadByte());
                    break;
                case OperandType.InlineVar:
                    instruction.Operand = GetVariable(instruction, ILStreamReader.ReadInt16());
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        void ResolveBranches()
        {
            foreach (var instruction in InstructionList)
            {
                switch (instruction.OpCode.OperandType)
                {
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.InlineBrTarget:
                        instruction.Operand = GetInstruction(InstructionList, (long)instruction.Operand);
                        break;
                    case OperandType.InlineSwitch:
                        var offsets = (int[])instruction.Operand;
                        var branches = new ILInstruction[offsets.Length];
                        for (int j = 0; j < offsets.Length; j++)
                        {
                            branches[j] = GetInstruction(InstructionList, offsets[j]);
                        }

                        instruction.Operand = branches;
                        break;
                }
            }
        }

        static ILInstruction GetInstruction(List<ILInstruction> instructions, long offset)
        {
            var size = instructions.Count;
            if (offset < 0 || offset > instructions[size - 1].Offset)
            {
                return null;
            }

            int min = 0;
            int max = size - 1;
            while (min <= max)
            {
                int mid = min + (max - min) / 2;
                var instruction = instructions[mid];
                var instruction_offset = instruction.Offset;

                if (offset == instruction_offset)
                {
                    return instruction;
                }

                if (offset < instruction_offset)
                {
                    max = mid - 1;
                }
                else
                {
                    min = mid + 1;
                }
            }

            return null;
        }

        object GetVariable(ILInstruction instruction, int index)
        {
            return TargetsLocalVariable(instruction.OpCode)
                ? GetLocalVariable(index) as object
                : GetParameter(index) as object;
        }

        static bool TargetsLocalVariable(OpCode opcode)
        {
            return opcode.Name.Contains("loc");
        }

        LocalVariableInfo GetLocalVariable(int index)
        {
            return LocalVariables[index];
        }

        ParameterInfo GetParameter(int index)
        {
            if (Method.IsStatic)
            {
                return Parameters[index];
            }

            if (index == 0)
            {
                return ThisInfo;
            }

            return Parameters[index - 1];
        }

        OpCode ReadOpCode()
        {
            byte op = ILStreamReader.ReadByte();
            return op != 0xfe
                ? OneByteOpCodes[op]
                : TwoBytesOpCodes[ILStreamReader.ReadByte()];
        }

        class ThisParameter : ParameterInfo
        {
            public ThisParameter(MethodBase method)
            {
                MemberImpl = method;
                ClassImpl = method.DeclaringType;
                NameImpl = "this";
                PositionImpl = -1;
            }
        }
    }
}
