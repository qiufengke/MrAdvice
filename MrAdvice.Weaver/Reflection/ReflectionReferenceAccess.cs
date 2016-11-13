#region Mr. Advice
// Mr. Advice
// A simple post build weaving package
// http://mradvice.arxone.com/
// Released under MIT license http://opensource.org/licenses/mit-license.php
#endregion

namespace ArxOne.MrAdvice.Reflection
{
    using System;

    [Flags]
    public enum ReflectionReferenceAccess
    {
        /// <summary>
        /// Invocation
        /// </summary>
        Call = 0x01,

        /// <summary>
        /// Read access
        /// </summary>
        Read = 0x10,

        /// <summary>
        /// Write access
        /// </summary>
        Write = 0x20,

        /// <summary>
        /// Guess what? Yes, read+write
        /// </summary>
        ReadWrite = Read | Write,
    }
}