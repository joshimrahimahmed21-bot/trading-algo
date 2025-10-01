using System;
using System.Collections.Generic;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class RollingStats
    {
        private int maxLength;
        private Queue<double> window;
        private double sum;
        private double sumSquares;

        public RollingStats(int length)
        {
            this.maxLength = length;
            this.window = new Queue<double>();
            this.sum = 0.0;
            this.sumSquares = 0.0;
        }

        public double Update(double value)
        {
            window.Enqueue(value);
            sum += value;
            sumSquares += value * value;
            if (window.Count > maxLength)
            {
                double removed = window.Dequeue();
                sum -= removed;
                sumSquares -= removed * removed;
            }
            return value;
        }

        public double Mean => window.Count > 0 ? sum / window.Count : 0.0;

        public double StandardDeviation
        {
            get
            {
                if (window.Count == 0) return 0.0;
                double mean = sum / window.Count;
                return Math.Sqrt(sumSquares / window.Count - mean * mean);
            }
        }

        public double UpdateAndZ(double value)
        {
            Update(value);
            double sd = StandardDeviation;
            if (sd == 0.0) return 0.0;
            return (value - Mean) / sd;
        }
    }

    public class Ema
    {
        private double alpha;
        private double ema;
        private bool initialized = false;

        public Ema(int length)
        {
            alpha = 2.0 / (length + 1);
            ema = 0.0;
        }

        public double Update(double value)
        {
            if (!initialized)
            {
                ema = value;
                initialized = true;
            }
            else
            {
                ema = alpha * value + (1 - alpha) * ema;
            }
            return ema;
        }
    }
}
