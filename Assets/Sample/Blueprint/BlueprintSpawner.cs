using System.Collections.Generic;
using UnityEngine;
using ZenECS.Core;
using ZenECS.Integration;

namespace ZenECS.Samples
{
    [AddComponentMenu("ZenECS/Samples/Blueprint Spawner")]
    public sealed class BlueprintSpawner : MonoBehaviour
    {
        [Header("Blueprint & View")]
        public EntityBlueprint Blueprint;
        public GameObject ViewPrefab;
        public bool AttachView = true;

        [Header("When/How Many")]
        public bool SpawnOnStart = true;
        public int Count = 1;

        [Header("Placement")]
        public Vector3 StartPosition = Vector3.zero;
        public Vector3 Spacing = new Vector3(2, 0, 0);
        public bool ArrangeGrid = false;
        public int GridColumns = 5;

        readonly List<(Entity entity, GameObject view)> _spawned = new();

        void OnEnable()
        {
            if (!SpawnOnStart) return;

            if (WorldRuntimeLocator.TryGet(out var world) && world != null)
            {
                SpawnNow(world);
            }
            else
            {
                // World가 준비되면 한 번만 스폰
                WorldRuntimeLocator.WorldSet += OnWorldReadyOnce;
            }
        }

        void OnDisable()
        {
            WorldRuntimeLocator.WorldSet -= OnWorldReadyOnce;
        }

        void OnWorldReadyOnce(World w)
        {
            WorldRuntimeLocator.WorldSet -= OnWorldReadyOnce;
            SpawnNow(w);
        }

        [ContextMenu("Spawn Now (if World ready)")]
        public void SpawnNowInEditor()
        {
            if (WorldRuntimeLocator.TryGet(out var world))
                SpawnNow(world);
            else
                Debug.LogWarning("[ZenECS] World not ready.");
        }

        void SpawnNow(World world)
        {
            if (Blueprint == null)
            {
                Debug.LogWarning("[ZenECS] Blueprint is null.");
                return;
            }

            for (int i = 0; i < Mathf.Max(1, Count); i++)
            {
                var pos = StartPosition;
                if (ArrangeGrid)
                {
                    int col = GridColumns <= 0 ? 1 : i % GridColumns;
                    int row = GridColumns <= 0 ? i : i / GridColumns;
                    pos = StartPosition + new Vector3(Spacing.x * col, Spacing.y * row, Spacing.z * row);
                }
                else
                {
                    pos = StartPosition + i * Spacing;
                }

                // View 생성 (선택)
                GameObject viewGo = null;
                if (AttachView)
                {
                    viewGo = ViewPrefab ? Instantiate(ViewPrefab) : new GameObject($"{Blueprint.name}_View_{i}");
                    viewGo.transform.SetPositionAndRotation(pos, Quaternion.identity);
                    viewGo.transform.localScale = Vector3.one;
                }

                // Blueprint → Entity
                var e = Blueprint.CreateEntityFromBlueprint(world, viewGo);

                // (선택) ViewSyncPolicy가 양방향이면 초기 위치/스케일을 View에서 데이터로 반영
                // Policy 기본값이 ‘양방향’이 아니라면, 필요 시 직접 Position/Rotation/Scale을 world에 써주면 됩니다.
                // if (world.Has<Position>(e))
                // {
                //     var p = world.Get<Position>(e);
                //     p.Value = pos;
                //     world.Set(e, in p);
                // }

                _spawned.Add((e, viewGo));
            }
        }
    }
}
