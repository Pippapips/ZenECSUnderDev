using System;
using UnityEngine;
using ZenECS.Core;

namespace ZenECS.Integration
{
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