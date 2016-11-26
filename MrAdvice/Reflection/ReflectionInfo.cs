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
    using System.Reflection;

    /// <summary>
    /// Reflection info about advised method
    /// </summary>
    public class ReflectionInfo
    {
        private static readonly IDictionary<Tuple<RuntimeMethodHandle, RuntimeTypeHandle>, ReflectionInfo> Infos
            = new Dictionary<Tuple<RuntimeMethodHandle, RuntimeTypeHandle>, ReflectionInfo>();

        internal static ReflectionInfo Get(RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle)
        {
            ReflectionInfo info;
            var key = Tuple.Create(methodHandle, typeHandle);
            Infos.TryGetValue(key, out info);
            if (info == null)
                Infos[key] = info = new ReflectionInfo();
            return info;
        }

        /// <summary>
        /// Gets the execution point.
        /// </summary>
        /// <value>
        /// The execution point.
        /// </value>
        public MethodInfo ExecutionPoint { get; internal set; }
    }
}
