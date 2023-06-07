using System;
using System.Collections.Generic;

namespace Mox.Extensions
{
    internal static class DictionaryExtensions
    {
        public static void TryAdd<T, U>(this Dictionary<T, U> @this, T key, U value)
        {

            if (@this.ContainsKey(key))
            {
                if (@this[key].Equals(value))
                {
                    return;
                }
                else
                {
                    throw new ArgumentException("An element with the same key but different value already exists in the dictionary.");
                }
            }

            @this.Add(key, value);
        }
    }
}
