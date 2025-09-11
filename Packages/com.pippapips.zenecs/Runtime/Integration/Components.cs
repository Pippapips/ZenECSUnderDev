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

    public struct Position : IComponent
    {
        public Vector3 Value;
    }

    public struct Rotation : IComponent
    {
        public Quaternion Value;
    }

    [Serializable]
    public struct ViewSyncPolicy : IComponent
    {
        public bool SyncPosition;
        public bool SyncRotation;
        public bool SyncScale;

        // true이면 뷰(Transform)의 변경을 데이터(컴포넌트)에 반영한다.
        // false이면 데이터->뷰 단방향만 수행.
        public bool Bidirectional;

        public static ViewSyncPolicy Default => new ViewSyncPolicy
        {
            SyncPosition = true,
            SyncRotation = false,
            SyncScale = false,
            Bidirectional = false
        };
    }
}