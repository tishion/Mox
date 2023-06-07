using System.Reflection;
using System.Reflection.Emit;

namespace Mox.Extensions
{
    internal static class ILGeneratorExtensionMethods
    {
        public static byte[] GetILBytes(this ILGenerator @this)
        {
            var bakeByteArray = typeof(ILGenerator).GetMethod("BakeByteArray", BindingFlags.Instance | BindingFlags.NonPublic);
            var ilBytes = bakeByteArray.Invoke(@this, null) as byte[];
            return ilBytes;
        }
    }
}