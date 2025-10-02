using System;
using System.Collections.Generic;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class MNQRSTest : Strategy
    {
    }

    public static class Helpers
    {
        public static double Clamp01(double x) =>
            (x < 0.0 ? 0.0 : (x > 1.0 ? 1.0 : x));
        public static double Squash(double x) =>
            0.5 * (Math.Tanh(x) + 1.0);
        public static double Blend(double a, double b, double w) =>
            (1 - w) * a + w * b;
    }

    public class RollingStats
    {
        private readonly int maxLength;
        private readonly Queue<double> window = new Queue<double>();
        public RollingStats(int length) { maxLength = length; }
        public double UpdateAndZ(double value)
        {
            window.Enqueue(value);
            if (window.Count > maxLength) window.Dequeue();
            int n = window.Count;
            if (n == 0) return 0.0;
            double sum = 0, sumSq = 0;
            foreach (double v in window) { sum += v; sumSq += v * v; }
            double mean = sum / n;
            double var = (sumSq / n) - (mean * mean);
            if (var < 1e-12) return 0.0;
            return (value - mean) / Math.Sqrt(var);
        }
    }

    public class Ema
    {
        private readonly double alpha;
        private bool hasValue;
        private double ema;
        public Ema(int period)
        {
            if (period < 1) period = 1;
            alpha = 2.0 / (period + 1);
        }
        public double Update(double value)
        {
            if (!hasValue)
            {
                ema = value;
                hasValue = true;
            }
            else
            {
                ema = alpha * value + (1 - alpha) * ema;
            }
            return ema;
        }
    }
}
