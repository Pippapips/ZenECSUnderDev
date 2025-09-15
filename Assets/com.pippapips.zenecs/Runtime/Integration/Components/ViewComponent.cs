using System;
using UnityEngine;
using ZenECS.Core;

namespace ZenECS.Integration
{
    public struct ViewComponent : IComponent
    {
        public GameObject Instance;

        // 시스템이 Data -> View 동기화를 마지막으로 수행한 Unity frame count (0 = never)
        // View->Data에서 동일 프레임의 재감지를 방지하기 위해 사용합니다.
        public int LastDataToViewFrame;
    }
}