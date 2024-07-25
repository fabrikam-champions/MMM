using System;

namespace MMM.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class SubscribesAttribute : MessageAttribute
    {
        public SubscribesAttribute(string messageName, Type messageType, string moduleName = null, string messageDescription = null) : base(messageName, messageType, moduleName, messageDescription)
        {
        }
    }
}