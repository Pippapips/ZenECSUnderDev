using UnityEngine;
using System;
using ZenECS.Core;
using ZenECS.Integration;

namespace ZenECS.Samples.TileTypes
{
    [CreateAssetMenu(fileName = "TileType_", menuName = "ZenECS/Tile Type", order = 100)]
    public sealed class TileType : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "Tile";

        [Header("Gameplay")]
        public bool Walkable = true;
        public float Cost = 1.0f;
        public float Height = 0.0f;
        public int DefenseCover = 0;           // 예: 덮기(cover) 값
        public float DetectionRadius = 0f;     // 네비게이션/AI용 범위

        [Header("View / VFX")]
        public GameObject ViewPrefab;          // 뷰 프리팹
        public Color DebugColor = Color.white;
        public GameObject LOD0Prefab;          // LOD 프리팹(선택)
        public GameObject LOD1Prefab;
        public AnimationClip IdleAnimation;    // 애니메이션 클립 레퍼런스
        public AnimationClip HoverAnimation;

        [Header("Tagging")]
        public string Category = "Default";    // 예: "Ground", "Water", "Road"

        [TextArea(2,6)]
        public string Notes;

        public TileProperties ToTileProperties()
        {
            return new TileProperties
            {
                Walkable = Walkable,
                Cost     = Cost,
                Height   = Height
            };
        }

        // 편의: ViewSyncPolicy 기본 생성
        public ViewSyncPolicy ToViewSyncPolicy()
        {
            return new ViewSyncPolicy
            {
                SyncPosition = true,
                SyncRotation = false,
                SyncScale    = false,
                Bidirectional = false
            };
        }
    }
}