﻿#region Mr. Advice
// Mr. Advice
// A simple post build weaving package
// http://mradvice.arxone.com/
// Released under MIT license http://opensource.org/licenses/mit-license.php
#endregion

namespace ArxOne.MrAdvice.Weaver
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using Advice;
    using Annotation;
    using dnlib.DotNet;
    using Pointcut;
    using Reflection;
    using Utility;

    /// <summary>
    /// Weaving context
    /// Allows to gather all common data
    /// </summary>
    internal class WeavingContext
    {
        /// <summary>
        /// Gets or sets the type of the <see cref="CompilerGeneratedAttribute"/>.
        /// </summary>
        /// <value>
        /// The type of the compiler generated attribute.
        /// </value>
        public ITypeDefOrRef CompilerGeneratedAttributeType { get; set; }

        /// <summary>
        /// Gets or sets the type of the <see cref="PriorityAttribute"/>.
        /// </summary>
        /// <value>
        /// The type of the priority attribute.
        /// </value>
        public ITypeDefOrRef PriorityAttributeType { get; set; }

        /// <summary>
        /// Gets or sets the type of the <see cref="AbstractTargetAttribute"/>.
        /// </summary>
        /// <value>
        /// The type of the abstract target attribute.
        /// </value>
        public ITypeDefOrRef AbstractTargetAttributeType { get; set; }

        /// <summary>
        /// Gets or sets the type of the <see cref="CollectDependenciesAttribute"/>.
        /// </summary>
        /// <value>
        /// The type of the collect dependencies attribute.
        /// </value>
        public ITypeDefOrRef CollectDependenciesAttributeType { get; set; }

        /// <summary>
        /// Gets or sets the type of the <see cref="IAdvice"/>.
        /// </summary>
        /// <value>
        /// The type of the weaving advice attribute.
        /// </value>
        public ITypeDefOrRef AdviceInterfaceType { get; set; }

        /// <summary>
        /// Gets or sets the type of the <see cref="IWeavingAdvice"/>.
        /// </summary>
        /// <value>
        /// The type of the weaving advice attribute.
        /// </value>
        public ITypeDefOrRef WeavingAdviceInterfaceType { get; set; }

        public TypeDef ShortcutClass { get; set; }

        /// <summary>
        /// Gets the currently added shortcut methods (shortcut to <see cref="Invocation.ProceedAdvice"/> method).
        /// </summary>
        /// <value>
        /// The shortcut methods.
        /// </value>
        public IDictionary<bool[], IMethod> ShortcutMethods { get; } = new Dictionary<bool[], IMethod>(new SequenceEqualityComparer<bool>());

        /// <summary>
        /// Gets or sets the <see cref="Invocation.ProceedAdvice"/> method.
        /// </summary>
        /// <value>
        /// The invocation proceed method.
        /// </value>
        public IMethod InvocationProceedMethod { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ExecutionPointAttribute"/> constructor.
        /// </summary>
        /// <value>
        /// The execution point attribute default ctor.
        /// </value>
        public MemberRef ExecutionPointAttributeDefaultCtor { get; set; }

        /// <summary>
        /// Gets the advices rules.
        /// </summary>
        /// <value>
        /// The advices rules.
        /// </value>
        public IDictionary<ITypeDefOrRef, PointcutSelector> AdvicesRules { get; } = new Dictionary<ITypeDefOrRef, PointcutSelector>(TypeComparer.Instance);

        /// <summary>
        /// Gets or sets the type of the <see cref="ExcludePointcutAttribute"/>.
        /// </summary>
        /// <value>
        /// The type of the exclude pointcut attribute.
        /// </value>
        public TypeDef ExcludePointcutAttributeType { get; set; }

        /// <summary>
        /// Gets or sets the type of the <see cref="IncludePointcutAttribute"/>.
        /// </summary>
        /// <value>
        /// The type of the include pointcut attribute.
        /// </value>
        public TypeDef IncludePointcutAttributeType { get; set; }

        /// <summary>
        /// Gets or sets the type of the exclude advice attribute.
        /// </summary>
        /// <value>
        /// The type of the exclude advice attribute.
        /// </value>
        public TypeDef ExcludeAdviceAttributeType { get; set; }

        private readonly IDictionary<MethodDef, MethodReflection> _reflection = new Dictionary<MethodDef, MethodReflection>(new MethodReferenceComparer());

        /// <summary>
        /// Gets the reflected methods.
        /// </summary>
        /// <value>
        /// The reflected methods.
        /// </value>
        public IEnumerable<KeyValuePair<MethodDef, MethodReflection>> ReflectedMethods => _reflection;

        public MethodReflection GetReflection(MethodDef method)
        {
            MethodReflection reflection;
            if (_reflection.TryGetValue(method, out reflection))
                return reflection;
            _reflection[method] = reflection = new MethodReflection(method);
            return reflection;
        }
    }
}
