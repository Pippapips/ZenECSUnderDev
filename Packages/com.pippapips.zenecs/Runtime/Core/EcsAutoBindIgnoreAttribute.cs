// Runtime/Core/EcsAutoBindIgnoreAttribute.cs

using System;

namespace ZenECS.Core
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class EcsAutoBindIgnoreAttribute : Attribute
    {
    }
}