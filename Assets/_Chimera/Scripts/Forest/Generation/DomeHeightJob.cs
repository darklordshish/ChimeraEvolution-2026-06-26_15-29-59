using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// Параллельный просчёт сетки высот павильона (s10a): IJobParallelFor без Burst-пакета
/// (в проекте нет com.unity.burst/mathematics — см. manifest; managed-воркеры всё равно
/// дают ~Nx на многоядерных, математика чисто C#). Один источник — DomeHeightField.SampleRaw.
/// </summary>
public struct DomeHeightJob : IJobParallelFor
{
    public DomeHeightField.DomeHeightParams p;
    public float x0;
    public float z0;
    public float step;
    public int res;

    [WriteOnly]
    public NativeArray<float> heights;

    public void Execute(int i)
    {
        int ix = i % res;
        int iz = i / res;
        heights[i] = DomeHeightField.SampleRaw(p, x0 + ix * step, z0 + iz * step);
    }
}
