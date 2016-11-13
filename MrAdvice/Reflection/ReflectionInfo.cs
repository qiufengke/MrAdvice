#region Mr. Advice
// Mr. Advice
// A simple post build weaving package
// http://mradvice.arxone.com/
// Released under MIT license http://opensource.org/licenses/mit-license.php
#endregion

namespace ArxOne.MrAdvice.Reflection
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Reflection info about advised method
    /// </summary>
    public class ReflectionInfo
    {
        internal static IDictionary<Tuple<IntPtr, IntPtr>, ReflectionInfo> Infos = new Dictionary<Tuple<IntPtr, IntPtr>, ReflectionInfo>();

        internal static ReflectionInfo Get(RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle)
        {
            throw new NotImplementedException();
        }
    }
}
