﻿#region Mr. Advice
// Mr. Advice
// A simple post build weaving package
// http://mradvice.arxone.com/
// Released under MIT license http://opensource.org/licenses/mit-license.php
#endregion
namespace ArxOne.MrAdvice.Weaver
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Advice;
    using Annotation;
    using dnlib.DotNet;
    using dnlib.DotNet.Emit;
    using dnlib.DotNet.Pdb;
    using Introduction;
    using Reflection;
    using Reflection.Groups;
    using Utility;
    using FieldAttributes = dnlib.DotNet.FieldAttributes;
    using MethodAttributes = dnlib.DotNet.MethodAttributes;
    using TypeAttributes = dnlib.DotNet.TypeAttributes;

    partial class AspectWeaver
    {
        /// <summary>
        /// Weaves the info advices for the given type.
        /// </summary>
        /// <param name="infoAdvisedType">Type of the module.</param>
        /// <param name="moduleDefinition">The module definition.</param>
        /// <param name="useWholeAssembly">if set to <c>true</c> [use whole assembly].</param>
        private void WeaveInfoAdvices(TypeDef infoAdvisedType, ModuleDef moduleDefinition, bool useWholeAssembly)
        {
            var invocationType = TypeResolver.Resolve(moduleDefinition, typeof(Invocation));
            if (invocationType == null)
                return;
            var proceedRuntimeInitializersReference = (from m in invocationType.Methods
                                                       where m.IsStatic && m.Name == nameof(Invocation.ProcessInfoAdvices)
                                                       let parameters = m.Parameters
                                                       where parameters.Count == 1
                                                             && parameters[0].Type.SafeEquivalent(
                                                                 moduleDefinition.SafeImport(useWholeAssembly ? typeof(Assembly) : typeof(Type)).ToTypeSig())
                                                       select m).SingleOrDefault();
            if (proceedRuntimeInitializersReference == null)
            {
                Logging.WriteWarning("Info advice method not found");
                return;
            }

            var instructions = GetCctorInstructions(infoAdvisedType);
            var proceedMethod = moduleDefinition.SafeImport(proceedRuntimeInitializersReference);

            if (useWholeAssembly)
                instructions.Emit(OpCodes.Call, moduleDefinition.SafeImport(ReflectionUtility.GetMethodInfo(() => Assembly.GetExecutingAssembly())));
            else
            {
                instructions.Emit(OpCodes.Ldtoken, TypeImporter.Import(moduleDefinition, infoAdvisedType.ToTypeSig()));
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                var getTypeFromHandleMethodInfo = ReflectionUtility.GetMethodInfo(() => Type.GetTypeFromHandle(new RuntimeTypeHandle()));
                instructions.Emit(OpCodes.Call, moduleDefinition.SafeImport(getTypeFromHandleMethodInfo));
            }
            instructions.Emit(OpCodes.Call, proceedMethod);
        }

        /// <summary>
        /// Returns a <see cref="Instructions"/> allowing to insert code in type .cctor.
        /// </summary>
        /// <param name="typeDef">The type definition.</param>
        /// <returns></returns>
        private Instructions GetCctorInstructions(TypeDef typeDef)
        {
            var moduleDefinition = typeDef.Module;
            const string cctorMethodName = ".cctor";
            var staticCtor = typeDef.Methods.SingleOrDefault(m => m.Name == cctorMethodName);
            if (staticCtor == null)
            {
                // the cctor needs to be called after all initialization (in case some info advices collect data)
                typeDef.Attributes &= ~TypeAttributes.BeforeFieldInit;
                var methodAttributes = (InjectAsPrivate ? MethodAttributes.Private : MethodAttributes.Public)
                                       | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
                staticCtor = new MethodDefUser(cctorMethodName, MethodSig.CreateStatic(moduleDefinition.CorLibTypes.Void), methodAttributes) { Body = new CilBody() };
                typeDef.Methods.Add(staticCtor);
                staticCtor.Body.Instructions.Add(new Instruction(OpCodes.Ret));
            }

            return new Instructions(staticCtor.Body.Instructions, staticCtor.Module);
        }

        /// <summary>
        /// Weaves the specified method.
        /// </summary>
        /// <param name="markedMethod">The marked method.</param>
        /// <param name="context">The context.</param>
        private void WeaveAdvices(MarkedNode markedMethod, WeavingContext context)
        {
            var method = markedMethod.Node.Method;

            // sanity check
            var moduleDefinition = (ModuleDefMD)method.Module;
            if (method.ReturnType.SafeEquivalent(moduleDefinition.CorLibTypes.Void))
            {
                var customAttributes = method.CustomAttributes;
                if (customAttributes.Any(c => c.AttributeType.Name == "AsyncStateMachineAttribute"))
                    Logging.WriteWarning("Advising async void method '{0}' could confuse async advices. Consider switching its return type to async Task.", method.FullName);
            }

            if (method.IsAbstract)
            {
                method.Attributes = (method.Attributes & ~MethodAttributes.Abstract) | MethodAttributes.Virtual;
                Logging.WriteDebug("Weaving abstract method '{0}'", method.FullName);
                WritePointcutBody(method, null, false, context);
            }
            else if (markedMethod.AbstractTarget)
            {
                Logging.WriteDebug("Weaving and abstracting method '{0}'", method.FullName);
                WritePointcutBody(method, null, true, context);
            }
            else
            {
                Logging.WriteDebug("Weaving method '{0}'", method.FullName);

                var methodName = method.Name;

                // create inner method
                const MethodAttributes attributesToKeep = MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.PInvokeImpl |
                                                          MethodAttributes.UnmanagedExport | MethodAttributes.HasSecurity |
                                                          MethodAttributes.RequireSecObject;
                var innerMethodAttributes = method.Attributes & attributesToKeep |
                                            (InjectAsPrivate ? MethodAttributes.Private : MethodAttributes.Public);
                string innerMethodName;
                if (method.IsGetter)
                    innerMethodName = GetPropertyInnerGetterName(GetPropertyName(methodName));
                else if (method.IsSetter)
                    innerMethodName = GetPropertyInnerSetterName(GetPropertyName(methodName));
                else
                    innerMethodName = GetInnerMethodName(methodName);
                var innerMethod = new MethodDefUser(innerMethodName, method.MethodSig, innerMethodAttributes);
                new MethodParameters(method).SetParamDefs(innerMethod);
                innerMethod.GenericParameters.AddRange(method.GenericParameters.Select(p => p.Clone(innerMethod)));
                innerMethod.ImplAttributes = method.ImplAttributes;
                innerMethod.SemanticsAttributes = method.SemanticsAttributes;
                if (method.IsPinvokeImpl)
                {
                    innerMethod.ImplMap = method.ImplMap;
                    method.ImplMap = null;
                    method.IsPreserveSig = false;
                    method.IsPinvokeImpl = false;
                }
                else
                {
                    innerMethod.Body = method.Body;
                    method.Body = new CilBody();
                }

                AddGeneratedAttribute(innerMethod, context);

                WritePointcutBody(method, innerMethod, false, context);
                lock (method.DeclaringType)
                    method.DeclaringType.Methods.Add(innerMethod);

                context.GetReflection(method).InnerMethod = innerMethod;
            }
        }

        private static void AddGeneratedAttribute(MethodDefUser innerMethod, WeavingContext context)
        {
            var generatedAttribute = new CustomAttribute(context.ExecutionPointAttributeDefaultCtor);
            innerMethod.CustomAttributes.Add(generatedAttribute);
        }

        /// <summary>
        /// Weaves method with weaving advices <see cref="IWeavingAdvice"/>.
        /// </summary>
        /// <param name="markedMethod">The marked method.</param>
        /// <param name="context">The context.</param>
        private void RunWeavingAdvices(MarkedNode markedMethod, WeavingContext context)
        {
            var method = markedMethod.Node.Method;
            var methodName = method.Name;

            // our special recipe, with weaving advices
            var weavingAdvicesMarkers = GetAllMarkers(markedMethod.Node, context.WeavingAdviceInterfaceType, context).ToArray();
            var typeDefinition = markedMethod.Node.Method.DeclaringType;
            var initialType = TypeLoader.GetType(typeDefinition);
            var weaverMethodWeavingContext = new WeaverMethodWeavingContext(typeDefinition, initialType, methodName, context, TypeResolver, Logging);
            foreach (var weavingAdviceMarker in weavingAdvicesMarkers)
            {
                Logging.WriteDebug("Weaving method '{0}' using weaving advice '{1}'", method.FullName, weavingAdviceMarker.Type.FullName);
                var weavingAdviceType = TypeLoader.GetType(weavingAdviceMarker.Type);
                var weavingAdvice = (IWeavingAdvice)Activator.CreateInstance(weavingAdviceType);
                var methodWeavingAdvice = weavingAdvice as IMethodWeavingAdvice;
                if (methodWeavingAdvice != null && !method.IsGetter && !method.IsSetter)
                    methodWeavingAdvice.Advise(weaverMethodWeavingContext);
            }
            if (weaverMethodWeavingContext.TargetMethodName != methodName)
                method.Name = weaverMethodWeavingContext.TargetMethodName;
        }

        /// <summary>
        /// Writes the pointcut body.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <param name="innerMethod">The inner method.</param>
        /// <param name="abstractedTarget">if set to <c>true</c> [abstracted target].</param>
        /// <param name="context">The context.</param>
        /// <exception cref="System.InvalidOperationException"></exception>
        private void WritePointcutBody(MethodDef method, MethodDef innerMethod, bool abstractedTarget, WeavingContext context)
        {
            var moduleDefinition = method.Module;

            // now empty the old one and make it call the inner method...
            if (method.Body == null)
                method.Body = new CilBody();
            method.Body.InitLocals = true;
            method.Body.Instructions.Clear();
            method.Body.Variables.Clear();
            method.Body.ExceptionHandlers.Clear();
            var instructions = new Instructions(method.Body.Instructions, method.Module);

            var targetArgument = GetTargetArgument(method);
            Local parametersVariable;
            var parametersArgument = GetParametersArgument(method, out parametersVariable);
            var methodArgument = GetMethodArgument(method);
            var innerMethodArgument = GetInnerMethodArgument(innerMethod);
            var typeArgument = GetTypeArgument(method);
            var abstractedArgument = GetAbstractedArgument(abstractedTarget);
            var genericParametersArgument = GetGenericParametersArgument(method);

            WriteProceedCall(instructions, context, targetArgument, parametersArgument, methodArgument, innerMethodArgument, typeArgument, abstractedArgument, genericParametersArgument);

            // get return value
            if (!method.ReturnType.SafeEquivalent(moduleDefinition.CorLibTypes.Void))
                instructions.EmitUnboxOrCastIfNecessary(method.ReturnType);
            else
                instructions.Emit(OpCodes.Pop); // if no return type, ignore Proceed() result

            // loads back out/ref parameters
            var methodParameters = new MethodParameters(method);
            for (int parameterIndex = 0; parameterIndex < methodParameters.Count; parameterIndex++)
            {
                var parameter = methodParameters[parameterIndex];
                if (parameter.Type is ByRefSig)
                {
                    instructions.EmitLdarg(parameter); // loads given parameter (it is a ref)
                    instructions.EmitLdloc(parametersVariable); // array
                    instructions.EmitLdc(parameterIndex); // array index
                    instructions.Emit(OpCodes.Ldelem_Ref); // now we have boxed out/ref value
                    var parameterElementType = parameter.Type.Next;
                    // TODO reimplement
                    if (parameterElementType.IsGenericInstanceType)
                    {
                        //var z = (GenericInstSig) parameterElementType;
                        //parameterElementType = z.GenericType;
                    }
                    //if (parameterElementType.IsGenericInstanceType) // a generic type requires the correct inner type
                    //{
                    //    var referenceParameterType = (ByReferenceType)parameter.ParameterType;
                    //    parameterElementType = (GenericInstanceType)referenceParameterType.ElementType;
                    //}
                    instructions.EmitUnboxOrCastIfNecessary(parameterElementType);
                    instructions.EmitStind(parameterElementType); // result is stored in ref parameter
                }
            }

            // and return
            instructions.Emit(OpCodes.Ret);

            method.Body.Scope = new PdbScope { Start = method.Body.Instructions[0] };
            method.Body.Scope.Scopes.Add(new PdbScope { Start = method.Body.Instructions[0] });
        }

        /// <summary>
        /// Writes the invocation call.
        /// </summary>
        /// <param name="instructions">The instructions.</param>
        /// <param name="context">The context.</param>
        /// <param name="arguments">The arguments.</param>
        /// <exception cref="InvalidOperationException">
        /// </exception>
        private void WriteProceedCall(Instructions instructions, WeavingContext context, params InvocationArgument[] arguments)
        {
            var proceedMethod = GetProceedMethod(arguments, instructions.Module, context);

            foreach (var argument in arguments)
            {
                if (argument.HasValue)
                    argument.Emit(instructions);
            }

            instructions.Emit(OpCodes.Call, proceedMethod);
        }

        private IMethod GetProceedMethod(InvocationArgument[] arguments, ModuleDef module, WeavingContext context)
        {
            var values = arguments.Select(a => a.HasValue).ToArray();
            IMethod proceedMethod;
            if (!context.ShortcutMethods.TryGetValue(values, out proceedMethod))
                context.ShortcutMethods[values] = proceedMethod = LoadProceedMethod(arguments, module, context);
            return proceedMethod;
        }

        private IMethod LoadProceedMethod(InvocationArgument[] arguments, ModuleDef module, WeavingContext context)
        {
            // special case, full invoke
            if (arguments.All(a => a.HasValue))
                return GetDefaultProceedMethod(module, context);

            return CreateProceedMethod(arguments, module, context);
        }

        private IMethod GetDefaultProceedMethod(ModuleDef module, WeavingContext context)
        {
            if (context.InvocationProceedMethod == null)
            {
                var invocationType = TypeResolver.Resolve(module, typeof(Invocation));
                if (invocationType == null)
                    throw new InvalidOperationException();
                var proceedMethodReference = invocationType.Methods.SingleOrDefault(m => m.IsStatic && m.Name == nameof(Invocation.ProceedAdvice));
                if (proceedMethodReference == null)
                    throw new InvalidOperationException();
                context.InvocationProceedMethod = module.SafeImport(proceedMethodReference);
            }
            return context.InvocationProceedMethod;
        }

        private IMethod CreateProceedMethod(InvocationArgument[] arguments, ModuleDef module, WeavingContext context)
        {
            // get the class from shortcuts
            var shortcutType = context.ShortcutClass;
            if (shortcutType == null)
            {
                shortcutType = new TypeDefUser("ArxOne.MrAdvice", "\u26A1Invocation")
                {
                    BaseType = module.Import(module.CorLibTypes.Object).ToTypeDefOrRef(),
                    // Abstract + Sealed is Static class
                    Attributes = TypeAttributes.NotPublic | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed
                };
                module.Types.Add(shortcutType);
                context.ShortcutClass = shortcutType;
            }

            // create the method
            var nameBuilder = new StringBuilder("ProceedAspect");
            var argumentIndex = 0;
            var methodSig = new MethodSig { RetType = module.CorLibTypes.Object, HasThis = false };
            var defaultProceedMethod = GetDefaultProceedMethod(module, context);
            foreach (var argument in arguments)
            {
                if (argument.HasValue)
                    methodSig.Params.Add(defaultProceedMethod.MethodSig.Params[argumentIndex]);
                // One day if there are arguments collision risks (IE optional arguments with same type), overload name
                argumentIndex++;
            }
            var method = new MethodDefUser(nameBuilder.ToString(), methodSig) { Body = new CilBody(), Attributes = MethodAttributes.Public | MethodAttributes.Static };
            shortcutType.Methods.Add(method);
            var instructions = new Instructions(method.Body.Instructions, module);

            // now, either get value from given arguments or from default
            argumentIndex = 0;
            var usedArgumentIndex = 0;
            foreach (var argument in arguments)
            {
                if (argument.HasValue) // a given argument
                    instructions.EmitLdarg(method.Parameters[usedArgumentIndex++]);
                else
                    arguments[argumentIndex].EmitDefault(instructions);
                argumentIndex++;
            }

            instructions.Emit(OpCodes.Tailcall); // because target method returns object and this method also returns an object
            instructions.Emit(OpCodes.Call, defaultProceedMethod);
            instructions.Emit(OpCodes.Ret);

            return method;
        }

        private InvocationArgument GetTargetArgument(MethodDef method)
        {
            var isStatic = method.IsStatic;
            return new InvocationArgument("This", !isStatic, delegate (Instructions i)
           {
               i.Emit(OpCodes.Ldarg_0);
               // to fix peverify 0x80131854
               if (method.IsConstructor)
                   i.Emit(OpCodes.Castclass, method.Module.CorLibTypes.Object);
           }, i => i.Emit(OpCodes.Ldnull));
        }

        private InvocationArgument GetParametersArgument(MethodDef method, out Local parametersVariable)
        {
            var methodParameters = new MethodParameters(method);
            var hasParameters = methodParameters.Count > 0;
            var localParametersVariable = parametersVariable = hasParameters ? new Local(new SZArraySig(method.Module.CorLibTypes.Object)) { Name = "parameters" } : null;
            return new InvocationArgument("Parameters", hasParameters,
                delegate (Instructions instructions)
                {
                    method.Body.Variables.Add(localParametersVariable);

                    instructions.EmitLdc(methodParameters.Count);
                    instructions.Emit(OpCodes.Newarr, method.Module.CorLibTypes.Object);
                    instructions.EmitStloc(localParametersVariable);
                    // setups parameters array
                    for (int parameterIndex = 0; parameterIndex < methodParameters.Count; parameterIndex++)
                    {
                        var parameter = methodParameters[parameterIndex];
                        // we don't care about output parameters
                        if (!parameter.ParamDef.IsOut)
                        {
                            instructions.EmitLdloc(localParametersVariable); // array
                            instructions.EmitLdc(parameterIndex); // array index
                            instructions.EmitLdarg(parameter); // loads given parameter...
                            var parameterType = parameter.Type;
                            if (parameterType is ByRefSig) // ...if ref, loads it as referenced value
                            {
                                parameterType = parameter.Type.Next;
                                instructions.EmitLdind(parameterType);
                            }
                            instructions.EmitBoxIfNecessary(parameterType); // ... and boxes it
                            instructions.Emit(OpCodes.Stelem_Ref);
                        }
                    }
                    instructions.EmitLdloc(localParametersVariable);
                }, instructions => instructions.Emit(OpCodes.Ldnull));
        }

        private InvocationArgument GetMethodArgument(MethodDef method)
        {
            return new InvocationArgument("Method", true, instructions => instructions.Emit(OpCodes.Ldtoken, method), null);
        }

        private InvocationArgument GetInnerMethodArgument(MethodDef innerMethod)
        {
            return new InvocationArgument("InnerMethod", innerMethod != null,
                instructions => instructions.Emit(OpCodes.Ldtoken, innerMethod),
                instructions => instructions.Emit(OpCodes.Dup));
        }

        private InvocationArgument GetTypeArgument(MethodDef method)
        {
            return new InvocationArgument("Type", method.DeclaringType.HasGenericParameters,
                instructions => instructions.Emit(OpCodes.Ldtoken, method.DeclaringType),
                instructions => instructions.Emit(OpCodes.Ldtoken, method.Module.CorLibTypes.Void));
        }

        private InvocationArgument GetAbstractedArgument(bool abstractedTarget)
        {
            return new InvocationArgument("Abstracted", abstractedTarget,
                i => i.Emit(OpCodes.Ldc_I4_1),
                i => i.Emit(OpCodes.Ldc_I4_0));
        }

        private InvocationArgument GetGenericParametersArgument(MethodDef method)
        {
            // on static methods from generic type, we also record the generic parameters type
            //var typeGenericParametersCount = isStatic ? method.DeclaringType.GenericParameters.Count : 0;
            var typeGenericParametersCount = method.DeclaringType.GenericParameters.Count;
            var hasGeneric = typeGenericParametersCount > 0 || method.HasGenericParameters;
            // if method has generic parameters, we also pass them to Proceed method
            var genericParametersVariable = hasGeneric ? new Local(new SZArraySig(method.Module.SafeImport(typeof(Type)).ToTypeSig())) { Name = "genericParameters" } : null;
            return new InvocationArgument("GenericArguments", hasGeneric,
                delegate (Instructions instructions)
                {
                    //IL_0001: ldtoken !!T
                    //IL_0006: call class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
                    method.Body.Variables.Add(genericParametersVariable);

                    instructions.EmitLdc(typeGenericParametersCount + method.GenericParameters.Count);
                    instructions.Emit(OpCodes.Newarr, method.Module.SafeImport(typeof(Type)));
                    instructions.EmitStloc(genericParametersVariable);

                    var methodGenericParametersCount = method.GenericParameters.Count;
                    for (int genericParameterIndex = 0; genericParameterIndex < typeGenericParametersCount + methodGenericParametersCount; genericParameterIndex++)
                    {
                        instructions.EmitLdloc(genericParametersVariable); // array
                        instructions.EmitLdc(genericParameterIndex); // array index
                        if (genericParameterIndex < typeGenericParametersCount)
                            instructions.Emit(OpCodes.Ldtoken, new GenericVar(genericParameterIndex, method.DeclaringType));
                        //genericParameters[genericParameterIndex]);
                        else
                            instructions.Emit(OpCodes.Ldtoken, new GenericMVar(genericParameterIndex - typeGenericParametersCount, method));
                        //genericParameters[genericParameterIndex]);
                        // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                        instructions.Emit(OpCodes.Call, ReflectionUtility.GetMethodInfo(() => Type.GetTypeFromHandle(new RuntimeTypeHandle())));
                        instructions.Emit(OpCodes.Stelem_Ref);
                    }
                    instructions.EmitLdloc(genericParametersVariable);
                }, instructions => instructions.Emit(OpCodes.Ldnull));
        }

        /// <summary>
        /// Weaves the introductions.
        /// Introduces members as requested by aspects
        /// </summary>
        /// <param name="method">The method.</param>
        /// <param name="moduleDefinition">The module definition.</param>
        /// <param name="context">The context.</param>
        private void WeaveIntroductions(MethodDef method, ModuleDef moduleDefinition, WeavingContext context)
        {
            var typeDefinition = method.DeclaringType;
            var advices = GetAllMarkers(new MethodReflectionNode(method, null), context.AdviceInterfaceType, context);
            var markerAttributeCtor = moduleDefinition.SafeImport(TypeResolver.Resolve(moduleDefinition, typeof(IntroducedFieldAttribute)).FindConstructors().Single());
            var markerAttributeCtorDef = new MemberRefUser(markerAttributeCtor.Module, markerAttributeCtor.Name, markerAttributeCtor.MethodSig, markerAttributeCtor.DeclaringType);
            foreach (var advice in advices)
            {
                var adviceDefinition = advice.Type;
                foreach (var field in adviceDefinition.Fields.Where(f => f.IsPublic))
                    IntroduceMember(method.Module, field.Name, field.FieldType.ToTypeDefOrRef(), field.IsStatic, advice.Type, typeDefinition, markerAttributeCtorDef);
                foreach (var property in adviceDefinition.Properties.Where(p => p.HasAnyPublic()))
                    IntroduceMember(method.Module, property.Name, property.PropertySig.RetType.ToTypeDefOrRef(), !property.PropertySig.HasThis, advice.Type, typeDefinition, markerAttributeCtorDef);
            }
        }

        /// <summary>
        /// Weaves the information advices.
        /// </summary>
        /// <param name="moduleDefinition">The module definition.</param>
        /// <param name="typeDefinition">The type definition.</param>
        /// <param name="infoAdviceInterface">The information advice interface.</param>
        /// <param name="context">The context.</param>
        private void WeaveInfoAdvices(ModuleDef moduleDefinition, TypeDef typeDefinition, ITypeDefOrRef infoAdviceInterface, WeavingContext context)
        {
            var markedMethods = GetMarkedMethods(new TypeReflectionNode(typeDefinition, null), infoAdviceInterface, context).ToArray();
            if (markedMethods.Where(IsWeavable).Any())
            {
                foreach (var markerMethod in markedMethods)
                    CollectDependencies(markerMethod, context);

                Logging.WriteDebug("Weaving type '{0}' for info", typeDefinition.FullName);
                WeaveInfoAdvices(typeDefinition, moduleDefinition, false);
            }
        }

        /// <summary>
        /// Weaves the method.
        /// </summary>
        /// <param name="moduleDefinition">The module definition.</param>
        /// <param name="markedMethod">The marked method.</param>
        /// <param name="context">The context.</param>
        private void WeaveMethod(ModuleDef moduleDefinition, MarkedNode markedMethod, WeavingContext context)
        {
            var method = markedMethod.Node.Method;
            try
            {
                CollectDependencies(markedMethod, context);
                WeaveAdvices(markedMethod, context);
                WeaveIntroductions(method, moduleDefinition, context);
            }
            catch (Exception e)
            {
                Logging.WriteError("Error while weaving method '{0}': {1}", method.FullName, e);
            }
        }

        /// <summary>
        /// Weaves the interface.
        /// What we do here is:
        /// - creating a class (wich is named after the interface name)
        /// - this class implements all interface members
        /// - all members invoke Invocation.ProcessInterfaceMethod
        /// </summary>
        /// <param name="moduleDefinition">The module definition.</param>
        /// <param name="interfaceType">Type of the interface.</param>
        /// <param name="context">The context.</param>
        private void WeaveInterface(ModuleDef moduleDefinition, TypeDef interfaceType, WeavingContext context)
        {
            Logging.WriteDebug("Weaving interface '{0}'", interfaceType.FullName);
            TypeDef implementationType;
            TypeDef advisedInterfaceType;
            TypeDef interfaceTypeDefinition;
            lock (moduleDefinition)
            {
                // ensure we're creating the interface only once
                var implementationTypeName = GetImplementationTypeName(interfaceType.Name);
                var implementationTypeNamespace = interfaceType.Namespace;
                if (moduleDefinition.GetTypes().Any(t => t.Namespace == implementationTypeNamespace && t.Name == implementationTypeName))
                    return;

                // now, create the implementation type
                var typeAttributes = (InjectAsPrivate ? TypeAttributes.NotPublic : TypeAttributes.Public) | TypeAttributes.Class | TypeAttributes.BeforeFieldInit;
                advisedInterfaceType = TypeResolver.Resolve(moduleDefinition, typeof(AdvisedInterface));
                // TODO: this should work using TypeImporter.Import
                var advisedInterfaceTypeReference = moduleDefinition.Import(advisedInterfaceType);
                implementationType = new TypeDefUser(implementationTypeNamespace, implementationTypeName, advisedInterfaceTypeReference) { Attributes = typeAttributes };
                implementationType.Interfaces.Add(new InterfaceImplUser(interfaceType));

                lock (moduleDefinition)
                    moduleDefinition.Types.Add(implementationType);
            }

            // create empty .ctor. This .NET mofo wants it!
            var baseEmptyConstructor = moduleDefinition.SafeImport(advisedInterfaceType.FindConstructors().Single());
            const MethodAttributes ctorAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            var method = new MethodDefUser(".ctor", baseEmptyConstructor.MethodSig, ctorAttributes);
            method.Body = new CilBody();
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, baseEmptyConstructor));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            implementationType.Methods.Add(method);

            // create implementation methods
            foreach (var interfaceMethod in interfaceType.Methods.Where(m => !m.IsSpecialName))
                WeaveInterfaceMethod(interfaceMethod, implementationType, true, context);

            // create implementation properties
            foreach (var interfaceProperty in interfaceType.Properties)
            {
                var implementationProperty = new PropertyDefUser(interfaceProperty.Name, interfaceProperty.PropertySig);
                implementationType.Properties.Add(implementationProperty);
                if (interfaceProperty.GetMethod != null)
                    implementationProperty.GetMethod = WeaveInterfaceMethod(interfaceProperty.GetMethod, implementationType, InjectAsPrivate, context);
                if (interfaceProperty.SetMethod != null)
                    implementationProperty.SetMethod = WeaveInterfaceMethod(interfaceProperty.SetMethod, implementationType, InjectAsPrivate, context);
            }

            // create implementation events
            foreach (var interfaceEvent in interfaceType.Events)
            {
                var implementationEvent = new EventDefUser(interfaceEvent.Name, interfaceEvent.EventType);
                implementationType.Events.Add(implementationEvent);
                if (interfaceEvent.AddMethod != null)
                    implementationEvent.AddMethod = WeaveInterfaceMethod(interfaceEvent.AddMethod, implementationType, InjectAsPrivate, context);
                if (interfaceEvent.RemoveMethod != null)
                    implementationEvent.RemoveMethod = WeaveInterfaceMethod(interfaceEvent.RemoveMethod, implementationType, InjectAsPrivate, context);
            }
        }

        /// <summary>
        /// Creates the advice wrapper and adds it to implementation.
        /// </summary>
        /// <param name="interfaceMethod">The interface method.</param>
        /// <param name="implementationType">Type of the implementation.</param>
        /// <param name="injectAsPrivate">if set to <c>true</c> [inject as private].</param>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        private MethodDef WeaveInterfaceMethod(MethodDef interfaceMethod, TypeDef implementationType, bool injectAsPrivate, WeavingContext context)
        {
            var methodAttributes = MethodAttributes.NewSlot | MethodAttributes.Virtual | (injectAsPrivate ? MethodAttributes.Public : MethodAttributes.Private);
            //var methodParameters = new MethodParameters(interfaceMethod);
            //var implementationMethodSig = new MethodSig(interfaceMethod.CallingConvention, (uint)interfaceMethod.GenericParameters.Count, interfaceMethod.ReturnType,
            //    methodParameters.Select(p => p.Type).ToArray())
            //{
            //    HasThis = interfaceMethod.HasThis,
            //    ExplicitThis = interfaceMethod.ExplicitThis,
            //    CallingConvention = interfaceMethod.CallingConvention,
            //};
            var implementationMethod = new MethodDefUser(interfaceMethod.Name, interfaceMethod.MethodSig /*implementationMethodSig*/, methodAttributes);
            //implementationMethod.ReturnType = interfaceMethod.ReturnType;
            implementationType.Methods.Add(implementationMethod);
            //implementationMethod.IsSpecialName = interfaceMethod.IsSpecialName;
            //methodParameters.SetParamDefs(implementationMethod);
            implementationMethod.GenericParameters.AddRange(interfaceMethod.GenericParameters);
            implementationMethod.Overrides.Add(new MethodOverride(implementationMethod, interfaceMethod));
            WritePointcutBody(implementationMethod, null, false, context);
            return implementationMethod;
        }

        /// <summary>
        /// Introduces the member.
        /// </summary>
        /// <param name="moduleDefinition">The module definition.</param>
        /// <param name="memberName">Name of the member.</param>
        /// <param name="memberType">Type of the member.</param>
        /// <param name="isStatic">if set to <c>true</c> [is static].</param>
        /// <param name="adviceType">The advice.</param>
        /// <param name="advisedType">The type definition.</param>
        /// <param name="markerAttribute">The marker attribute ctor.</param>
        private void IntroduceMember(ModuleDef moduleDefinition, string memberName, ITypeDefOrRef memberType, bool isStatic,
            ITypeDefOrRef adviceType, TypeDef advisedType, ICustomAttributeType markerAttribute)
        {
            ITypeDefOrRef introducedFieldType;
            if (IsIntroduction(memberType, out introducedFieldType))
            {
                var introducedFieldName = IntroductionRules.GetName(adviceType.Namespace, adviceType.Name, memberName);
                lock (advisedType.Fields)
                {
                    if (advisedType.Fields.All(f => f.Name != introducedFieldName))
                    {
                        var fieldAttributes = (InjectAsPrivate ? FieldAttributes.Private : FieldAttributes.Public) | FieldAttributes.NotSerialized;
                        if (isStatic)
                            fieldAttributes |= FieldAttributes.Static;
                        Logging.WriteDebug("Introduced field type '{0}'", introducedFieldType.FullName);
                        var introducedFieldTypeReference = TypeImporter.Import(moduleDefinition, introducedFieldType.ToTypeSig());
                        var introducedField = new FieldDefUser(introducedFieldName, new FieldSig(introducedFieldTypeReference), fieldAttributes);
                        introducedField.CustomAttributes.Add(new CustomAttribute(markerAttribute));
                        advisedType.Fields.Add(introducedField);
                    }
                }
            }
        }
    }
}
