using System.Reflection.Emit;
using System.Text;

namespace Mox.Decompiler
{
    internal sealed class ILInstruction
    {
        long offset;
        OpCode opcode;
        object operand;

        ILInstruction previous;
        ILInstruction next;

        public long Offset
        {
            get { return offset; }
        }

        public OpCode OpCode
        {
            get { return opcode; }
        }

        public object Operand
        {
            get { return operand; }
            internal set { operand = value; }
        }

        public ILInstruction Previous
        {
            get { return previous; }
            internal set { previous = value; }
        }

        public ILInstruction Next
        {
            get { return next; }
            internal set { next = value; }
        }

        public int Size
        {
            get
            {
                int size = opcode.Size;

                switch (opcode.OperandType)
                {
                    case OperandType.InlineSwitch:
                        size += (1 + ((ILInstruction[])operand).Length) * 4;
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        size += 8;
                        break;
                    case OperandType.InlineBrTarget:
                    case OperandType.InlineField:
                    case OperandType.InlineI:
                    case OperandType.InlineMethod:
                    case OperandType.InlineString:
                    case OperandType.InlineTok:
                    case OperandType.InlineType:
                    case OperandType.ShortInlineR:
                        size += 4;
                        break;
                    case OperandType.InlineVar:
                        size += 2;
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        size += 1;
                        break;
                }

                return size;
            }
        }

        internal ILInstruction(long offset, OpCode opcode)
        {
            this.offset = offset;
            this.opcode = opcode;
        }

        public override string ToString()
        {
            var instruction = new StringBuilder();

            AppendLabel(instruction, this);
            instruction.Append(':');
            instruction.Append(' ');
            instruction.Append(opcode.Name);

            if (operand == null)
            {
                return instruction.ToString();
            }

            instruction.Append(' ');

            switch (opcode.OperandType)
            {
                case OperandType.ShortInlineBrTarget:
                case OperandType.InlineBrTarget:
                    AppendLabel(instruction, (ILInstruction)operand);
                    break;
                case OperandType.InlineSwitch:
                    var labels = (ILInstruction[])operand;
                    for (int i = 0; i < labels.Length; i++)
                    {
                        if (i > 0)
                        {
                            instruction.Append(',');
                        }

                        AppendLabel(instruction, labels[i]);
                    }
                    break;
                case OperandType.InlineString:
                    instruction.Append('\"');
                    instruction.Append(operand);
                    instruction.Append('\"');
                    break;
                default:
                    instruction.Append(operand);
                    break;
            }

            return instruction.ToString();
        }

        static void AppendLabel(StringBuilder builder, ILInstruction instruction)
        {
            builder.Append("IL_");
            builder.Append(instruction.offset.ToString("x4"));
        }
    }
}
