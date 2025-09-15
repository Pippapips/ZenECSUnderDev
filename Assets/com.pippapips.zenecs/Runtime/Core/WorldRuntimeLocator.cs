namespace ZenECS.Core
{
    public static class WorldRuntimeLocator
    {
        static World _world;
        public static event System.Action<World> WorldSet;   // ★ 추가

        public static void Set(World world)
        {
            _world = world;
            WorldSet?.Invoke(world);                        // ★ 준비 완료 알림
        }

        public static bool TryGet(out World world)
        { 
            world = _world; 
            return world != null; 
        }
    }
}