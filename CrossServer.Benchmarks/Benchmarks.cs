using System;
using BenchmarkDotNet;
using BenchmarkDotNet.Attributes;
using CrossServer.Shared;

namespace CrossServer.Benchmarks
{
    [MemoryDiagnoser]
    public class Benchmarks
    {
        [Params(100, 1000, 10000)]
        public int Numbers;

        [GlobalSetup]
        public void Setup()
        {
        }
        
        [Benchmark]
        public void PacketSerialization()
        {

        }
        
        [Benchmark]
        public void PacketDeserialization()
        {
        }
    }
}