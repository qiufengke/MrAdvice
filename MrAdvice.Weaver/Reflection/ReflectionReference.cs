#region Mr. Advice
// Mr. Advice
// A simple post build weaving package
// http://mradvice.arxone.com/
// Released under MIT license http://opensource.org/licenses/mit-license.php
#endregion

namespace ArxOne.MrAdvice.Reflection
{
    using System.Diagnostics;
    using dnlib.DotNet;
    using Utility;

    [DebuggerDisplay("{DebugLiteral}")]
    public class ReflectionReference
    {
        /// <summary>
        /// Gets the access.
        /// </summary>
        /// <value>
        /// The access.
        /// </value>
        public ReflectionReferenceAccess Access { get; }

        /// <summary>
        /// Gets the method.
        /// </summary>
        /// <value>
        /// The method.
        /// </value>
        public IMethodDefOrRef Method { get; }

        /// <summary>
        /// Gets the property.
        /// </summary>
        /// <value>
        /// The property.
        /// </value>
        public PropertyDef Property { get; }

        /// <summary>
        /// Gets the field.
        /// </summary>
        /// <value>
        /// The field.
        /// </value>
        public IField Field { get; }

        private string DebugLiteral
        {
            get
            {
                if (Field != null)
                    return $"Field {Field.Name}, {Access}";
                return $"Method {Method.Name}, {Access}";
            }
        }

        public ReflectionReference(IMethodDefOrRef method)
        {
            Access = ReflectionReferenceAccess.Call;
            Method = method;
        }

        public ReflectionReference(IMethodDefOrRef method, PropertyDef property)
        {
            Access = method.SafeEquivalent(property.GetMethod) ? ReflectionReferenceAccess.Read : ReflectionReferenceAccess.Write;
            Method = method;
            Property = property;
        }

        public ReflectionReference(IField field, ReflectionReferenceAccess access)
        {
            Field = field;
            Access = access;
        }
    }
}
