using BenchmarkDotNet.Attributes;
using Blastzone.Simulation.Numerics;

namespace Blastzone.Benchmarks;

/// <summary>
/// Each benchmark processes <see cref="Count"/> values so per-call overhead is amortised;
/// divide the reported time by Count (BenchmarkDotNet does this via OperationsPerInvoke).
/// The double versions are baselines for comparison only.
/// </summary>
[MemoryDiagnoser]
public class FixedBenchmarks
{
    const int Count = 1024;
    readonly Fixed[] _a = new Fixed[Count];
    readonly Fixed[] _b = new Fixed[Count];
    readonly double[] _da = new double[Count];
    readonly double[] _db = new double[Count];

    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < Count; i++)
        {
            _a[i] = Fixed.FromRatio(i * 37 - 9000, 97);
            _b[i] = Fixed.FromRatio(i * 13 + 5, 31);
            _da[i] = _a[i].Raw / 4294967296.0;
            _db[i] = _b[i].Raw / 4294967296.0;
        }
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public long FixedMul()
    {
        long acc = 0;
        for (var i = 0; i < Count; i++)
            acc += (_a[i] * _b[i]).Raw;
        return acc;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public double DoubleMul()
    {
        double acc = 0;
        for (var i = 0; i < Count; i++)
            acc += _da[i] * _db[i];
        return acc;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public long FixedDiv()
    {
        long acc = 0;
        for (var i = 0; i < Count; i++)
            acc += (_a[i] / _b[i]).Raw;
        return acc;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public long FixedSin()
    {
        long acc = 0;
        for (var i = 0; i < Count; i++)
            acc += FixedMath.Sin(_a[i]).Raw;
        return acc;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public double DoubleSin()
    {
        double acc = 0;
        for (var i = 0; i < Count; i++)
            acc += Math.Sin(_da[i]);
        return acc;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public long FixedSqrt()
    {
        long acc = 0;
        for (var i = 0; i < Count; i++)
            acc += FixedMath.Sqrt(_b[i]).Raw;
        return acc;
    }
}
