using System;

namespace KrayonCore
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class ToStorageAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class NoSerializeToInspectorAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public sealed class CallEventAttribute : Attribute
    {
        public string? DisplayName { get; set; }

        public CallEventAttribute()
        {
        }

        public CallEventAttribute(string displayName)
        {
            DisplayName = displayName;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class RangeAttribute : Attribute
    {
        public float Min { get; }
        public float Max { get; }

        public RangeAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }
}