using System;
using System.Reflection;

namespace Mox.Core
{
    internal class ExceptionHandler
    {
        public Type CatchType;

        public ExceptionHandlingClauseOptions Flags;

        public long TryStart;

        public long TryEnd;

        public long FilterStart;

        public long HandlerStart;

        public long HandlerEnd;
    }
}
