﻿using System;

namespace MMM.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class PublishesAttribute : MessageAttribute
    {
        public PublishesAttribute(string messageName, Type messageType, string moduleName = null, string messageDescription = null) : base(messageName, messageType, moduleName, messageDescription)
        {
        }
    }
}