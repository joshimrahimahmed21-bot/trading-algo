using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Session and volume profile management for MNQRSTest.  Provides
    /// parameters and helpers for weighting quality metrics by time of day
    /// relative to a session anchor and stub volume profile logic.  In a
    /// complete strategy this partial would also manage trail switching
    /// and runner scaling based on volume profile congestion; here we
    /// include only a simplified adjustment.
    /// </summary>
    public enum SessionCentersMode { Off = 0, FixedAnchor = 1, Adaptive = 2 }

    public partial class MNQRSTest : Strategy
    {
        // === Session anchor and weighting properties ===
        [NinjaScriptProperty]
        [Display(Name = "UseSessionAnchor", GroupName = "Session", Order = 10)]
        public bool UseSessionAnchor { get; set; }

        [NinjaScriptProperty]
        [Range(0, 23)]
        [Display(Name = "AnchorHour", GroupName = "Session", Order = 11)]
        public int AnchorHour { get; set; }

        [NinjaScriptProperty]
        [Range(0, 59)]
        [Display(Name = "AnchorMinute", GroupName = "Session", Order = 12)]
        public int AnchorMinute { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "SessionWindowShape", GroupName = "Session", Order = 13)]
        public string SessionWindowShape { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 10.0)]
        [Display(Name = "PreScale", GroupName = "Session", Order = 14)]
        public double PreScale { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 10.0)]
        [Display(Name = "PostScale", GroupName = "Session", Order = 15)]
        public double PostScale { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, 1440.0)]
        [Display(Name = "AnchorWindowMins", GroupName = "Session", Order = 16)]
        public double AnchorWindowMins { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "SessionCentersMode", GroupName = "Session", Order = 17)]
        public SessionCentersMode SessionCentersModeParamV2 { get; set; }

        // internal anchor cache
        private DateTime? sessionAnchorUtc;

        /// <summary>
        /// Compute a weighting factor for the current bar based on its
        /// distance in minutes from the session anchor.  Weighting is
        /// disabled if UseSessionAnchor=false or mode=Off.  Supported
        /// window shapes are box, tri/triangular or Gaussian (default).
        /// </summary>
        private double Session_WeightNow()
        {
            if (!UseSessionAnchor || SessionCentersModeParamV2 == SessionCentersMode.Off)
                return 1.0;
            // initialize anchor time if not already set
            if (!sessionAnchorUtc.HasValue)
            {
                int h = Math.Max(0, Math.Min(23, AnchorHour));
                int m = Math.Max(0, Math.Min(59, AnchorMinute));
                var today = Times[0][0].Date;
                sessionAnchorUtc = new DateTime(today.Year, today.Month, today.Day, h, m, 0);
            }
            DateTime barTime = Times[0][0];
            double deltaMins = Math.Abs((barTime - sessionAnchorUtc.Value).TotalMinutes);
            double window = Math.Max(1.0, AnchorWindowMins);
            string shape = (SessionWindowShape ?? "gaussian").Trim().ToLowerInvariant();
            double core;
            if (shape == "box")
                core = deltaMins <= window ? 1.0 : 0.0;
            else if (shape.StartsWith("tri"))
                core = Math.Max(0.0, 1.0 - (deltaMins / window));
            else
            {
                double sigma = window / 2.0;
                core = Math.Exp(-(deltaMins * deltaMins) / (2.0 * sigma * sigma));
            }
            double w = core;
            w *= (PreScale > 0.0 ? PreScale : 1.0);
            w = Math.Min(1.0, Math.Max(0.0, w));
            w *= (PostScale > 0.0 ? PostScale : 1.0);
            return Math.Min(1.0, Math.Max(0.0, w));
        }

        /// <summary>
        /// Update volume profile context.  In this compile‑safe stub the
        /// tailwind, headwind and cushion signals are set to zero and
        /// congestion is neutral.  A complete implementation would pull
        /// directional bias from a volume profile indicator such as QVP.
        /// </summary>
        private void UpdateVPContext()
        {
            lastQ_VP_Tailwind = 0.0;
            lastQ_VP_Headwind = 0.0;
            lastQ_VP_Cushion = 0.0;
            lastVP_Congestion = 0.0;
        }

        /// <summary>
        /// Apply in‑trade adjustments based on volume profile signals.  If
        /// UseVPRunnerScaling and UseVolumeProfile are enabled the runner
        /// percentage is nudged by a small amount derived from the
        /// difference between tailwind and headwind.  Hysteresis and
        /// trail switching are not implemented here.  The adjustment is
        /// limited to ±0.2 per call.
        /// </summary>
        private void ApplyVPManagementAdjustments()
        {
            if (UseVPRunnerScaling && UseVolumeProfile)
            {
                double bias = lastQ_VP_Tailwind - lastQ_VP_Headwind;
                double k1 = VP_RunnerK1Param;
                double k2 = VP_RunnerK2Param;
                // compute adjustment using k1 and k2 (k2 acts opposite to k1)
                double adj = k1 * bias - k2 * bias;
                // clip adjustment to ±0.2
                adj = Math.Max(-0.2, Math.Min(0.2, adj));
                lastRunnerPct = Helpers.Clamp01(lastRunnerPct + adj);
            }
        }
    }
}