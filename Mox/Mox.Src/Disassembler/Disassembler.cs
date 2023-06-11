using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Mox.Disassembler
{
    public static class Disassembler
    {
        public static IList<Instruction> GetInstructions(this MethodBase self)
        {
            if (self == null)
                throw new ArgumentNullException("self");

            return MethodBodyReader.GetInstructions(self).AsReadOnly();
        }

        public static IList<Instruction> GetInstructions(this DynamicMethod self, Type[] arguments = null, IList<LocalVariableInfo> locals = null)
        {
            if (self == null)
                throw new ArgumentNullException("self");

            return MethodBodyReader.GetInstructions(self, arguments, locals).AsReadOnly();
        }
    }
}
