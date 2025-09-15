using ZenECS.Core;

public static class WorldComponentExtensions
{
    public static bool Has<T>(this World w, Entity e)
        where T : struct, IComponent
        => w.GetPool<T>().Has(e);

    public static T Get<T>(this World w, Entity e)
        where T : struct, IComponent
        => w.GetPool<T>().Get(e);              // struct 복사본 반환

    public static bool TryGet<T>(this World w, Entity e, out T value)
        where T : struct, IComponent
    {
        var p = w.GetPool<T>();
        if (p.Has(e)) { value = p.Get(e); return true; }
        value = default; return false;
    }

    public static void Set<T>(this World w, Entity e, in T value)
        where T : struct, IComponent
        => w.AddOrSet(e, value);

    public static bool Remove<T>(this World w, Entity e)
        where T : struct, IComponent
        => w.GetPool<T>().Remove(e);
}