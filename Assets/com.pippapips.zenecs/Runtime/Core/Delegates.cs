namespace ZenECS.Core
{
    // ref 쓰기용
    public delegate void RefAction<TA, TB>(TA a, ref TB b);
    public delegate void RefAction<TA, TB, TC>(TA a, ref TB b, ref TC c);

    // 읽기 전용(in) 최적화용 (원하면 사용)
    public delegate void InAction<TA, TB>(TA a, in TB b);
    public delegate void InAction<TA, TB, TC>(TA a, in TB b, in TC c);
}