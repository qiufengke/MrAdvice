#region Mr. Advice
// Mr. Advice
// A simple post build weaving package
// http://mradvice.arxone.com/
// Released under MIT license http://opensource.org/licenses/mit-license.php
#endregion

namespace ArxOne.MrAdvice.Reflection
{
    using System;

    /// <summary>
    /// Reflection entry points
    /// </summary>
    public static class Registry
    {
        private static readonly RuntimeTypeHandle VoidTypeHandle = typeof(void).TypeHandle;

        /// <summary>
        /// Sets the execution point.
        /// </summary>
        /// <param name="methodHandle">The method handle.</param>
        /// <param name="typeHandle">The type handle.</param>
        /// <param name="executionPointHandle">The execution point handle.</param>
        public static void SetExecutionPoint(RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle, RuntimeMethodHandle executionPointHandle)
        {

        }

        /// <summary>
        /// Sets the execution point.
        /// </summary>
        /// <param name="methodHandle">The method handle.</param>
        /// <param name="executionPointHandle">The execution point handle.</param>
        public static void SetExecutionPoint(RuntimeMethodHandle methodHandle, RuntimeMethodHandle executionPointHandle)
        {
            SetExecutionPoint(methodHandle, VoidTypeHandle, executionPointHandle);
        }
    }
}
