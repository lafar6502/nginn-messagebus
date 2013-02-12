using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Linq.Expressions;

namespace NGinnBPM.MessageBus.Impl
{
    /// <summary>
    /// LateBoundMethod is a generic method signature that is passed an instance
    /// and an array of parameters and returns an object. It basically can be 
    /// used to call any method.
    /// 
    /// </summary>
    /// <param name="target">The instance that the dynamic method is called on</param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public delegate object LateBoundMethod(object target, object[] arguments);
    /// <summary>
    /// Delegate for message 'Handle' method
    /// </summary>
    /// <param name="target"></param>
    /// <param name="message"></param>
    public delegate void MessageHandlerMethodDelegate(object target, object message);
    /// <summary>
    /// Delegate for IMessageHandlerService.Handle
    /// </summary>
    /// <param name="target"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public delegate object ServiceHandlerMethodDelegate(object target, object message);

    /// <summary>
    /// This class creates a generic method delegate from a MethodInfo signature
    /// converting the method call into a LateBoundMethod delegate call. Using
    /// this class allows making repeated calls very quickly.
    /// 
    /// Note: this class will be very inefficient for individual dynamic method
    /// calls - compilation of the expression is very expensive up front, so using
    /// this delegate factory makes sense only if you re-use and cache the dynamicly 
    /// loaded method repeatedly.
    /// 
    /// Entirely based on Nate Kohari's blog post:
    /// http://kohari.org/2009/03/06/fast-late-bound-invocation-with-expression-trees/
    /// </summary>
    public static class DelegateFactory
    {

        /// <summary>
        /// Creates a LateBoundMethod delegate from a MethodInfo structure
        /// Basically creates a dynamic delegate on the fly.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static LateBoundMethod Create(MethodInfo method)
        {
            ParameterExpression instanceParameter = Expression.Parameter(typeof(object), "target");
            ParameterExpression argumentsParameter = Expression.Parameter(typeof(object[]), "arguments");

            MethodCallExpression call = Expression.Call(
                Expression.Convert(instanceParameter, method.DeclaringType),
                method,
                CreateParameterExpressions(method, argumentsParameter));

            Expression<LateBoundMethod> lambda = Expression.Lambda<LateBoundMethod>(
                Expression.Convert(call, typeof(object)),
                instanceParameter,
                argumentsParameter);

            return lambda.Compile();
        }

        /// <summary>
        /// Create a delegate for message handler (Handle) method of a generic interface
        /// Used by message bus for delivering messages to their handlers.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static MessageHandlerMethodDelegate CreateMessageHandlerDelegate(MethodInfo method)
        {
            ParameterExpression instanceParameter = Expression.Parameter(typeof(object), "target");
            ParameterExpression argumentsParameter = Expression.Parameter(typeof(object), "message");

            MethodCallExpression call = Expression.Call(
                Expression.Convert(instanceParameter, method.DeclaringType),
                method,
                Expression.Convert(argumentsParameter, method.GetParameters()[0].ParameterType)
                );
            Expression<MessageHandlerMethodDelegate> lambda = Expression.Lambda<MessageHandlerMethodDelegate>(
                call,
                instanceParameter,
                argumentsParameter);
            return lambda.Compile();
        }

        public static ServiceHandlerMethodDelegate CreateServiceHandlerDelegate(MethodInfo method)
        {
            ParameterExpression instanceParameter = Expression.Parameter(typeof(object), "target");
            ParameterExpression argumentsParameter = Expression.Parameter(typeof(object), "message");

            MethodCallExpression call = Expression.Call(
                Expression.Convert(instanceParameter, method.DeclaringType),
                method,
                Expression.Convert(argumentsParameter, method.GetParameters()[0].ParameterType)
                );
            Expression<ServiceHandlerMethodDelegate> lambda = Expression.Lambda<ServiceHandlerMethodDelegate>(
                call,
                instanceParameter,
                argumentsParameter);
            return lambda.Compile();
        }

        public static LateBoundMethod CreateConstructorInvocation(ConstructorInfo ci)
        {
            ParameterExpression instanceParameter = Expression.Parameter(typeof(object), "target");
            ParameterExpression argumentsParameter = Expression.Parameter(typeof(object[]), "arguments");

            var call = Expression.New(
                ci,
                CreateParameterExpressions(ci, argumentsParameter));

            Expression<LateBoundMethod> lambda = Expression.Lambda<LateBoundMethod>(
                Expression.Convert(call, typeof(object)),
                instanceParameter,
                argumentsParameter);

            return lambda.Compile();


        }
        

        /// <summary>
        /// Creates a LateBoundMethod from type methodname and parameter signature that
        /// is turned into a MethodInfo structure and then parsed into a dynamic delegate
        /// </summary>
        /// <param name="type"></param>
        /// <param name="methodName"></param>
        /// <param name="parameterTypes"></param>
        /// <returns></returns>
        public static LateBoundMethod Create(Type type, string methodName, params Type[] parameterTypes)
        {
            return Create(type.GetMethod(methodName, parameterTypes));
        }

        private static Expression[] CreateParameterExpressions(MethodBase method, Expression argumentsParameter)
        {
            return method.GetParameters().Select((parameter, index) =>
                Expression.Convert(
                    Expression.ArrayIndex(argumentsParameter, Expression.Constant(index)),
                    parameter.ParameterType)).ToArray();
        }
    }
}
