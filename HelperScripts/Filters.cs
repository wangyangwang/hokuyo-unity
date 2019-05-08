using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HKY
{
    interface IFilter<T>
    {
        T[] Filter(T[] input, int period);
    }

    public class MovingAverage : IFilter<long>
    {
        public long[] Filter(long[] data, int period)
        {
            long[] buffer = new long[period];
            long[] output = new long[data.Length];
            int current_index = 0;
            for (int i = 0; i < data.Length; i++)
            {
                buffer[current_index] = data[i] / period;
                long ma = 0;
                for (int j = 0; j < period; j++)
                {
                    ma += buffer[j];
                }
                output[i] = ma;
                current_index = (current_index + 1) % period;
            }
            return output;
        }
    }
}