using System;
using System.Reflection;

namespace MMM.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class SubscribesAttribute : MessageAttribute
    {
        public SubscribesAttribute(string messageName, Type messageType, string moduleName = null, string messageDescription = null) : base(messageName, messageType, moduleName, messageDescription)
        {
        }
    }
}