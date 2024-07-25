using System;

namespace MMM.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public abstract class MessageAttribute : Attribute
    {
        private readonly string _MessageName;
        private readonly Type _MessageType;
        private readonly string _MessageDescription;
        private readonly string _ModuleName;

        public string MessageName => _MessageName;
        public Type MessageType => _MessageType;
        public string MessageDescription => _MessageDescription;
        public string ModuleName => _ModuleName;

        public MessageAttribute(string messageName, Type messageType, string moduleName = null, string messageDescription = null)
        {
            this._MessageName = messageName;
            this._MessageType = messageType;
            this._ModuleName = moduleName;
            this._MessageDescription = messageDescription;
        }
    }
}
