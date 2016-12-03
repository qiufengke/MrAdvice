#region Mr. Advice
// Mr. Advice
// A simple post build weaving package
// http://mradvice.arxone.com/
// Released under MIT license http://opensource.org/licenses/mit-license.php
#endregion

namespace ArxOne.MrAdvice.Reflection
{
    using System.Collections.Generic;
    using dnlib.DotNet;

    public class MethodReflection
    {
        /// <summary>
        /// Gets the method.
        /// </summary>
        /// <value>
        /// The method.
        /// </value>
        public MethodDef Method { get; }

        /// <summary>
        /// Gets or sets the inner method.
        /// </summary>
        /// <value>
        /// The inner method.
        /// </value>
        public MethodDef InnerMethod { get; set; }

        /// <summary>
        /// Gets or sets the references.
        /// </summary>
        /// <value>
        /// The references.
        /// </value>
        public List<ReflectionReference> References { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MethodReflection"/> class.
        /// </summary>
        /// <param name="method">The method.</param>
        public MethodReflection(MethodDef method)
        {
            Method = method;
        }
    }
}
