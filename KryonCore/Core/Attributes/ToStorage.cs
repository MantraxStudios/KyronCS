using System;

namespace KrayonCore
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class ToStorageAttribute : Attribute
    {
    }
}