using System;
using System.Collections.Generic;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Common helper methods and simple numeric utilities used by the MNQRSTest
    /// strategy.  These helpers are defined in a separate static class to
    /// centralize functions such as clamping, squashing and blending.  Rolling
    /// statistics and EMA classes are also provided for smoothing and
    /// z‑score calculations.  By centralizing these helpers we avoid
    /// duplicate definitions across partial strategy files.
    /// </summary>
    public static class Helpers
    {
        /// <summary>
        /// Clamp a value into the inclusive range [0,1].  Negative values
        /// become 0 and values greater than 1 become 1.
        /// </summary>
        public static double Clamp01(double x) => x < 0.0 ? 0.0 : (x > 1.0 ? 1.0 : x);

        /// <summary>
        /// Non‑linear squash function mapping unbounded values into (0,1).
        /// Uses a hyperbolic tangent; 0 maps to 0.5, large positive values
        /// approach 1 and large negatives approach 0.
        /// </summary>
        public static double Squash(double x) => 0.5 * (Math.Tanh(x) + 1.0);

        /// <summary>
        /// Blend two values by weight w (0..1).  When w is 0 returns a,
        /// when w is 1 returns b and linear blend in between.
        /// </summary>
        public static double Blend(double a, double b, double w) => (1.0 - w) * a + w * b;
    }

    /// <summary>
    /// Maintains a fixed‑length window of values and provides an updated
    /// z‑score for each new value.  This is useful for detecting outliers
    /// or measuring the strength of a signal relative to recent history.
    /// </summary>
    public class RollingStats
    {
        private readonly int maxLength;
        private readonly Queue<double> window;

        public RollingStats(int length)
        {
            maxLength = length;
            window = new Queue<double>();
        }

        /// <summary>
        /// Add a new value to the rolling window and return its z‑score
        /// relative to the current window.  If insufficient samples exist
        /// the z‑score will be zero.  A small epsilon guards against
        /// division by zero.
        /// </summary>
        public double UpdateAndZ(double value)
        {
            window.Enqueue(value);
            if (window.Count > maxLength)
                window.Dequeue();
            int n = window.Count;
            if (n == 0)
                return 0.0;
            double sum = 0.0, sumSq = 0.0;
            foreach (double v in window)
            {
                sum += v;
                sumSq += v * v;
            }
            double mean = sum / n;
            double var = (sumSq / n) - (mean * mean);
            if (var < 1e-12)
                return 0.0;
            double stdDev = Math.Sqrt(var);
            return (value - mean) / stdDev;
        }
    }

    /// <summary>
    /// Simple exponential moving average for smoothing a series of values.
    /// The smoothing factor alpha is derived from the specified period.
    /// </summary>
    public class Ema
    {
        private readonly double alpha;
        private bool hasValue;
        private double ema;
        public Ema(int period)
        {
            if (period < 1) period = 1;
            alpha = 2.0 / (period + 1.0);
            hasValue = false;
            ema = 0.0;
        }
        /// <summary>
        /// Incorporate a new sample into the EMA and return the updated
        /// value.  On the first call the EMA is initialized to the sample.
        /// </summary>
        public double Update(double value)
        {
            if (!hasValue)
            {
                ema = value;
                hasValue = true;
            }
            else
            {
                ema = alpha * value + (1.0 - alpha) * ema;
            }
            return ema;
        }
    }
}