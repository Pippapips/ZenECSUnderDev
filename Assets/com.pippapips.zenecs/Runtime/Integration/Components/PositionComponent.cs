using System;
using UnityEngine;
using ZenECS.Core;

namespace ZenECS.Integration
{
    public struct Position : IComponent
    {
        public Vector3 Value;
    }
}