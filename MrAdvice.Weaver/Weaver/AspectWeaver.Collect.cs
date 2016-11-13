#region Mr. Advice
// Mr. Advice
// A simple post build weaving package
// http://mradvice.arxone.com/
// Released under MIT license http://opensource.org/licenses/mit-license.php
#endregion

namespace ArxOne.MrAdvice.Weaver
{
    using System.Collections.Generic;
    using Annotation;
    using dnlib.DotNet;
    using dnlib.DotNet.Emit;
    using Reflection;
    using Utility;

    partial class AspectWeaver
    {
        private void CollectDependencies(MarkedNode markedMethod, WeavingContext context)
        {
            if (!markedMethod.CollectDependencies)
                return;

            var reflection = context.GetReflection(markedMethod.Node.Method);
            if (reflection.References != null)
                return;

            reflection.References = new List<ReflectionReference>();
            CollectDependencies(reflection, markedMethod.Node.Method.Body?.Instructions);
            CollectDependencies(reflection, reflection.InnerMethod?.Body?.Instructions);
        }

        private void CollectDependencies(MethodReflection reflection, IList<Instruction> instructions)
        {
            if (instructions == null)
                return;

            foreach (var instruction in instructions)
            {
                switch (instruction.OpCode.Code)
                {
                    // fields here, simple

                    case Code.Ldfld:
                    case Code.Ldsfld:
                        reflection.References.Add(new ReflectionReference((IField)instruction.Operand, ReflectionReferenceAccess.Read));
                        break;
                    case Code.Ldflda:
                    case Code.Ldsflda:
                        reflection.References.Add(new ReflectionReference((IField)instruction.Operand, ReflectionReferenceAccess.ReadWrite));
                        break;
                    case Code.Stfld:
                    case Code.Stsfld:
                        reflection.References.Add(new ReflectionReference((IField)instruction.Operand, ReflectionReferenceAccess.Write));
                        break;

                    // the other option is method call
                    default:
                        var method = instruction.Operand as IMethodDefOrRef;
                        if (method != null)
                        {
                            var methodDef = method.ResolveMethodDef();
                            bool setter;
                            var relatedProperty = methodDef?.GetProperty(out setter);
                            if (relatedProperty != null)
                                reflection.References.Add(new ReflectionReference(methodDef, relatedProperty));
                            else
                                reflection.References.Add(new ReflectionReference(method));
                        }
                        break;
                }
            }
        }
    }
}
