using System;
using UnityEngine;
using ZenECS.Core;

namespace ZenECS.Integration
{
    public struct Rotation : IComponent
    {
        public Quaternion Value;
    }
}