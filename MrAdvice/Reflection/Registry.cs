#region Mr. Advice
// Mr. Advice
// A simple post build weaving package
// http://mradvice.arxone.com/
// Released under MIT license http://opensource.org/licenses/mit-license.php
#endregion

namespace ArxOne.MrAdvice.Reflection
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Reflection entry points.
    /// Ideas:
    /// - Methods written in target assembly provide shortcuts to methods below
    /// - Shortcuts take parameters: firsts are method/type handle, then other parameters:
    ///   - execution point (method handle)
    ///   - referenced fields handles (for local fields on non generic types)
    ///   - referenced fields+types handles
    ///   - invoked methods handles (for local methods on non generic types)
    ///   - invoked mehohds+types handles
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
            var executionPointMethod = typeHandle.Equals(VoidTypeHandle)
                ? MethodBase.GetMethodFromHandle(executionPointHandle)
                : MethodBase.GetMethodFromHandle(executionPointHandle, typeHandle);
            // cast is safe, execution point is never a ctor (it is always an inner method)
            ReflectionInfo.Get(methodHandle, typeHandle).ExecutionPoint = (MethodInfo)executionPointMethod;
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

        /// <summary>
        /// Adds the referenced fields.
        /// </summary>
        /// <param name="methodHandle">The method handle.</param>
        /// <param name="typeHandle">The type handle.</param>
        /// <param name="fields">The fields.</param>
        /// <param name="fieldsTypes">The fields types.</param>
        public static void AddReferencedFields(RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle,
            RuntimeFieldHandle[] fields, RuntimeTypeHandle[] fieldsTypes)
        {
            ReflectionInfo.Get(methodHandle, typeHandle).
        }
    }
}
