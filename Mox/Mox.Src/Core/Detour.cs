using System;
using System.Reflection;
using Mox.Utils;

namespace Mox
{
    internal partial class Detour
    {
        internal Isolate Isolate;

        internal object Target { get; }

        internal MethodBase Method { get; }

        internal Delegate Replacement { get; private set; }

        public Detour(Isolate isolate, MethodBase method, object target)
        {
            this.Isolate = isolate;
            this.Method = method;
            this.Target = target;
        }

        private Isolate ReplaceWithImpl(Delegate replacement)
        {
            MethodHelper.ValidateMethodSignature(this.Method, replacement.Method);
            this.Replacement = replacement;
            return this.Isolate;
        }
    }
}
