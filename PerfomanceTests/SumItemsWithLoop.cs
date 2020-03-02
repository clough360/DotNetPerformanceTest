using System;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;

namespace PerformanceTests
{
    [Orderer(SummaryOrderPolicy.FastestToSlowest, MethodOrderPolicy.Declared)]
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    [RPlotExporter, RankColumn]
    public class SumItemsWithLoop
    {
        private int[] _items;
        private int _sum = 0;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _items = new int[ItemCount];
            var r = new Random(1);
            for (var i = 0; i < ItemCount; i++)
            {
                var item = r.Next(100);
                _items[i] = item;
                _sum += item;
            }
        }

        private void CheckSum(int sum)
        {
            if (sum != _sum)
            {
                throw new Exception("sum is incorrect");
            }
        }

        [Params(100000)]
        public int ItemCount;


        [Benchmark(Baseline = true)]
        public void SumArrayWithForEachLoop()
        {
            var sum = 0;
            foreach (var v in _items)
            {
                sum += v;
            }

            CheckSum(sum);
        }

        [Benchmark]
        public void SumArrayWithForLoop()
        {
            var sum = 0;
            for (var i = 0; i < ItemCount; i++)
            {
                sum += _items[i];
            }
            CheckSum(sum);
        }

        [Benchmark]
        public void SumArrayWithWhileLoop()
        {
            var sum = 0;
            var i = 0;
            while (i < ItemCount) 
            {
                sum += _items[i++];
            }
            CheckSum(sum);
        }

        // this loop takes advantage of the fact that a CPU can perform four basic operations in a cycle
        [Benchmark]
        public void SumArrayWithWhileLoopUnrolled()
        {
            var sum = 0;
            var i = 0;

            var lastLoopIndex = ItemCount - ItemCount % 4;
            while (i < lastLoopIndex)
            {
                sum += _items[i];
                sum += _items[i+1];
                sum += _items[i+2];
                sum += _items[i+3];
                i+=4;
            }

            while (i < ItemCount)
            {
                sum += _items[i++];
            }
            CheckSum(sum);
        }

        /// <summary>
        /// perform unrolled loop, but pin the array to avoid array bounds check
        /// </summary>
        [Benchmark]
        public unsafe void SumArrayWithWhileLoopUnrolledPinned()
        {
            var sum = 0;
            var i = 0;

            fixed (int* pItems = _items)
            {

                var lastLoopIndex = ItemCount - ItemCount % 4;
                while (i < lastLoopIndex)
                {
                    sum += pItems[i];
                    sum += pItems[i + 1];
                    sum += pItems[i + 2];
                    sum += pItems[i + 3];
                    i += 4;
                }

                while (i < ItemCount)
                {
                    sum += pItems[i++];
                }
            }

            CheckSum(sum);
        }

        /// <summary>
        /// uses vector / simd intrinsics
        /// </summary>
        [Benchmark]
        public void SumArrayWithSimd()
        {
            var sum = 0;
            var i = 0;

            var itemsSpan = new ReadOnlySpan<int>(_items);
            var sumVectors = Vector<int>.Zero;
            var lastBlockIndex = _items.Length - (_items.Length % Vector<int>.Count);

            // sum the list (unrolled with vector)
            while (i < lastBlockIndex)
            {
                sumVectors += new Vector<int>(itemsSpan.Slice(i));
                i += Vector<int>.Count;
            }

            // sum the vector results
            for (var n = 0; n < Vector<int>.Count; n++)
            {
                sum += sumVectors[n];
            }

            // sum the remaining items
            while (i < itemsSpan.Length)
            {
                sum += itemsSpan[i];
                i += 1;
            }

            CheckSum(sum);
        }

        /// <summary>
        /// perform sum using span
        /// </summary>
        [Benchmark]
        public void SumArrayWithSpan()
        {
            var sum = 0;
            var i = 0;

            var itemsSpan = new ReadOnlySpan<int>(_items);

            while (i < itemsSpan.Length)
            {
                sum += itemsSpan[i++];
            }

            CheckSum(sum);
        }

        /// <summary>
        /// perform unrolled loop, using span
        /// </summary>
        [Benchmark]
        public void SumArrayWithSpanUnrolled()
        {
            var sum = 0;
            var i = 0;

            var itemsSpan = new ReadOnlySpan<int>(_items);
            var lastLoopIndex = ItemCount - ItemCount % 4;
            while (i < lastLoopIndex)
            {
                sum += itemsSpan[i];
                sum += itemsSpan[i + 1];
                sum += itemsSpan[i + 2];
                sum += itemsSpan[i + 3];
                i += 4;
            }

            while (i < ItemCount)
            {
                sum += itemsSpan[i++];
            }

            CheckSum(sum);
        }

        /// <summary>
        /// perform vectorised sum using hardware intrinsics
        /// </summary>
        [Benchmark]
        public unsafe void SumVectorizedHardwareSse2()
        {
            if (!Sse2.IsSupported)
            {
                return;
            }

            int sum;

            fixed (int* pItems = _items)
            {
                var resultVector = Vector128<int>.Zero;

                var i = 0;
                var lastBlockIndex = _items.Length - (_items.Length % 4);

                // sum unrolled block with vectors
                while (i < lastBlockIndex)
                {
                    resultVector = Sse2.Add(resultVector, Sse2.LoadVector128(pItems + i));
                    i += 4;
                }

                if (Ssse3.IsSupported)
                {
                    resultVector = Ssse3.HorizontalAdd(resultVector, resultVector);
                    resultVector = Ssse3.HorizontalAdd(resultVector, resultVector);
                }
                else
                {
                    resultVector = Sse2.Add(resultVector, Sse2.Shuffle(resultVector, 0x4E));
                    resultVector = Sse2.Add(resultVector, Sse2.Shuffle(resultVector, 0xB1));
                }
                sum = resultVector.ToScalar();

                // sum the remaining items
                while (i < _items.Length)
                {
                    sum += pItems[i];
                    i += 1;
                }
            }

            CheckSum(sum);
        }
    }
}
