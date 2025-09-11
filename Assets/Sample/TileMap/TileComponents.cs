using System;
using UnityEngine;

namespace ZenECS.Core
{
    // 그리드 좌표 (정수 X,Z)
    public struct TileIndex : IComponent
    {
        public int X;
        public int Z;

        public TileIndex(int x, int z) { X = x; Z = z; }
    }

    // 타일 속성
    [Serializable]
    public struct TileProperties : IComponent
    {
        public bool Walkable;
        public float Cost; // 이동 코스트 (1.0 기본)
        public float Height; // 지형 높이 (선택)

        public static TileProperties Default => new TileProperties { Walkable = true, Cost = 1f, Height = 0f };
    }

    // 이웃 엔티티 id 목록 (int[] 는 편의상 사용)
    public struct TileNeighbors : IComponent
    {
        public int[] NeighborEntityIds; // 엔티티 Id 목록 (보통 4방향 또는 8방향)
    }
}