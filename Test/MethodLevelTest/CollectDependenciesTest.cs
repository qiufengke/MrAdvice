#region Mr. Advice
// Mr. Advice
// A simple post build weaving package
// http://mradvice.arxone.com/
// Released under MIT license http://opensource.org/licenses/mit-license.php
#endregion

namespace MethodLevelTest
{
    using System;
    using ArxOne.MrAdvice.Advice;
    using ArxOne.MrAdvice.Annotation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [CollectDependencies]
    public class CollectingAdvice : Attribute, IMethodInfoAdvice
    {
        public void Advise(MethodAdviceContext context)
        {
            context.Proceed();
        }

        public void Advise(MethodInfoAdviceContext context)
        {
        }
    }

    public class CollectedClass
    {
        public int Field;

        public int Property { get; set; }

        public void Method()
        { }

        [CollectingAdvice]
        public void SimpleMethodCall()
        {
            Method();
        }

        [CollectingAdvice]
        public void SimpleMethodRefCall()
        {
            Action method = Method;
        }

        [CollectingAdvice]
        public void PropertyReader()
        {
            var z = Property;
        }

        [CollectingAdvice]
        public void PropertyWriter()
        {
            Property = 12;
        }

        [CollectingAdvice]
        public void FieldReader()
        {
            var x = Field;
        }

        [CollectingAdvice]
        public void FieldWriter()
        {
            Field = 56;
        }

        private void Out(out int f)
        {
            f = 34;
        }

        private void Ref(ref int f)
        {
            f++;
        }

        [CollectingAdvice]
        public void FieldOut()
        {
            Out(out Field);
        }

        [CollectingAdvice]
        public void FieldRef()
        {
            Ref(ref Field);
        }
    }

    [TestClass]
    public class CollectDependenciesTest
    {
        [TestMethod]
        [TestCategory("CollectDependencies")]
        public void SimpleMethodCallTest()
        { }
    }
}
