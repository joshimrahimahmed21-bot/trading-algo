using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using System;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript.Indicators;



namespace NinjaTrader.NinjaScript.Strategies
{
    // Session center modes for weighting around anchor times.
    public enum SessionCentersMode { Off = 0, FixedAnchor = 1, Adaptive = 2 }

    public partial class MNQRSTest : Strategy
    {
        private double lastRunnerPct = 0.0;
        private double lastRunnerBasePct = 0.0;
        private double lastQ_VP_Tailwind = 0.0;
        private double lastQ_VP_Headwind = 0.0;
        private double lastQ_VP_Cushion = 0.0;
        // Session anchor/centering parameters
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
        [Display(Name = "SessionCentersMode", GroupName = "Session", Order = 50)]
        public SessionCentersMode SessionCentersModeParamV2 { get; set; } = SessionCentersMode.Off;

        // Legacy string shim property for backwards compatibility.  Converts a free-form
        // string into the corresponding SessionCentersMode.
        [Display(Name = "sessioncentersmode", GroupName = "Session (Legacy Shim)", Order = 999)]
        public string sessioncentersmode
        {
            get { return SessionCentersModeParamV2.ToString(); }
            set
            {
                string s = (value ?? string.Empty).Trim().ToLowerInvariant();
                if (s == "fixed" || s == "fixedanchor" || s == "1")
                    SessionCentersModeParamV2 = SessionCentersMode.FixedAnchor;
                else if (s == "adaptive" || s == "2")
                    SessionCentersModeParamV2 = SessionCentersMode.Adaptive;
                else
                    SessionCentersModeParamV2 = SessionCentersMode.Off;
            }
        }
    }
}

    public partial class MNQRSTest : Strategy
    {
        // internal caches (no UI exposure)
        private DateTime? sessionAnchorUtc;

        // Call once per session or whenever anchors may change
        private void Session_InitIfNeeded()
        {
            if (sessionAnchorUtc.HasValue) return;

            // Resolve primary session anchor today (wall-clock from props, fallback safe)
            int h = Math.Max(0, Math.Min(23, AnchorHour));
            int m = Math.Max(0, Math.Min(59, AnchorMinute));
            // Convert to exchange time if you prefer; using Bars.GetTime(0) basis for simplicity
            var today = Times[0][0].Date;
            sessionAnchorUtc = new DateTime(today.Year, today.Month, today.Day, h, m, 0);
        }

        // Main: 0..1 weight for current bar
        private double Session_WeightNow()
        {
            Session_InitIfNeeded();
            var mode = SessionCentersModeParamV2;          // Off / FixedAnchor / Adaptive
            if (!UseSessionAnchor || mode == SessionCentersMode.Off)
                return 1.0;

            // distance in minutes from anchor
            DateTime barTime = Times[0][0];
            double deltaMins = Math.Abs((barTime - sessionAnchorUtc.Value).TotalMinutes);

            // choose window shape
            string shape = (SessionWindowShape ?? "Gaussian").Trim().ToLowerInvariant();
            double pre  = Math.Max(0.0, PreScale);
            double post = Math.Max(0.0, PostScale);
            double window = Math.Max(1.0, AnchorWindowMins);

            double core;
            if (shape == "box")
                core = deltaMins <= window ? 1.0 : 0.0;
            else if (shape == "tri" || shape == "triangular")
                core = Math.Max(0.0, 1.0 - (deltaMins / window));
            else
            {
                // Gaussian-like
                double sigma = window / 2.0; if (sigma < 1e-6) sigma = 1.0;
                core = Math.Exp(-(deltaMins * deltaMins) / (2.0 * sigma * sigma));
            }

            // pre/post scaling (soft clamp)
            double w = core * (pre > 0 ? pre : 1.0);
            w = Math.Min(1.0, Math.Max(0.0, w));
            w *= (post > 0 ? post : 1.0);
            return Math.Min(1.0, Math.Max(0.0, w));
        }
    }
}
    public partial class MNQRSTest : Strategy
    {
        // VP context signals
        private double tailwind;
        private double headwind;
        
        private void UpdateVPContext()
        {
            // Compute Tailwind/Headwind from volume profile (using QVP indicator outputs).
            // **Stub:** not implemented – default to 0 (neutral).
            tailwind = 0.0;
            headwind = 0.0;
            // (In future, set tailwind/headwind e.g. based on price vs value area, LVN/HVN, etc.)
        }
    }
}
// ===== VP Overlay (non-destructive) =====
    public partial class MNQRSTest : Strategy
    {
        // Use unique helper names to avoid collisions with your existing ones
        private static double VPClamp01(double x) => x < 0 ? 0 : (x > 1 ? 1 : x);

        /// <summary>Lightweight overlap% congestion proxy. Only use if you don’t already compute congestion elsewhere.</summary>
        private double VPComputeOverlapPct(int lookbackBars)
        {
            try
            {
                int n = Math.Max(2, Math.Min(lookbackBars, CurrentBar));
                double hi = double.MinValue, lo = double.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (High[i] > hi) hi = High[i];
                    if (Low[i]  < lo) lo = Low[i];
                }
                double range = hi - lo;
                if (range <= Instrument.MasterInstrument.TickSize) return 1.0;

                int small = 0;
                for (int i = 0; i < n; i++)
                {
                    double body = Math.Abs(Close[i] - Open[i]);
                    if (body <= 0.25 * range) small++;
                }
                return VPClamp01((double)small / n);
            }
            catch { return 0.5; }
        }

        // isolated state to avoid clashing with your existing vars
        private int  _vp2SwitchCooldownBars = 0;
        private int  _vp2DebounceCount = 0;
        private bool _vp2PreferATR = false;

        /// <summary>
        /// Overlay adjustments that PRESERVE existing values unless allowOverride=true.
        /// - If your code already computed Tailwind/Headwind/Cushion/Congestion, we keep them.
        /// - We only fill gaps (NaN/Infinity), or override when explicitly allowed.
        /// - Trail switch uses its own small state (_vp2*) so it won’t fight your existing switch.
        /// </summary>
        private void ApplyVP_OverlayAdjustmentsSafe(bool isLong, bool allowOverride = false)
        {
            // Use your existing directional components if present
            double pos = (double.IsNaN(lastQPosRes) || double.IsInfinity(lastQPosRes)) ? 0.0 : lastQPosRes;
            double neg = (double.IsNaN(lastQNegRes) || double.IsInfinity(lastQNegRes)) ? 0.0 : lastQNegRes;

            // Fill directional scores only if missing or override requested
            bool twMissing = double.IsNaN(lastQ_VP_Tailwind) || double.IsInfinity(lastQ_VP_Tailwind);
            bool hwMissing = double.IsNaN(lastQ_VP_Headwind) || double.IsInfinity(lastQ_VP_Headwind);
            bool cuMissing = double.IsNaN(lastQ_VP_Cushion)  || double.IsInfinity(lastQ_VP_Cushion);

            if (allowOverride || twMissing || hwMissing || cuMissing)
            {
                double tail = isLong ? pos : neg;
                double cush = isLong ? neg : pos;
                double head = 1.0 - VPClamp01(tail);

                if (allowOverride || twMissing) lastQ_VP_Tailwind = VPClamp01(tail);
                if (allowOverride || cuMissing) lastQ_VP_Cushion  = VPClamp01(cush);
                if (allowOverride || hwMissing) lastQ_VP_Headwind = VPClamp01(head);

                // Composite directional bias (telemetry only)
                lastQ_ResVP_dir = VPClamp01(
                    0.6 * lastQ_VP_Tailwind +
                    0.4 * lastQ_VP_Cushion -
                    0.5 * lastQ_VP_Headwind
                );
            }

            // Congestion: only fill if missing/zero-ish or override
            bool cgMissing = double.IsNaN(lastVP_Congestion) || double.IsInfinity(lastVP_Congestion) || lastVP_Congestion == 0.0;
            if (UseVPTrailSwitch && (allowOverride || cgMissing))
            {
                int lookbackBars = MinutesToPrimaryBars(Math.Max(5, Math.Min(VPCongestionLookbackMins, VP_ShortWindow)));
                double cong = VPComputeOverlapPct(lookbackBars);
                lastVP_Congestion = VPClamp01(cong);
                lastVP_RegimeLabel = (lastVP_Congestion >= VPClamp01(VPCongestionThresh)) ? "Basic" : "Directional";
            }

            // Trail switch (independent small state so we don’t fight your existing logic)
            if (UseVPTrailSwitch)
            {
                bool congestedNow  = lastVP_Congestion >= VPClamp01(VPCongestionThresh);
                bool preferATRWant = !congestedNow;

                if (preferATRWant == _vp2PreferATR)
                {
                    _vp2DebounceCount = 0;
                }
                else
                {
                    _vp2DebounceCount++;
                    if (_vp2DebounceCount >= 20 && _vp2SwitchCooldownBars == 0)
                    {
                        _vp2PreferATR = preferATRWant;
                        _vp2DebounceCount = 0;
                        _vp2SwitchCooldownBars = Math.Max(10, HysteresisBars);
                    }
                }
                if (_vp2SwitchCooldownBars > 0) _vp2SwitchCooldownBars--;

                // Only set labels/types if caller permits override or they’re empty
                if (allowOverride || string.IsNullOrEmpty(lastTrailLabel))
                {
                    lastTrailType  = _vp2PreferATR ? QVP.TrailType.ATR : QVP.TrailType.SR;
                    lastTrailLabel = _vp2PreferATR ? "ATR" : "SR";
                }
            }

            // Runner scaling (safe, small centered nudge). We respect existing lastRunnerPct as base.
            if (UseVPRunnerScaling)
            {
                double k1 = VPClamp01(VP_RunnerK1);
                double k2 = VPClamp01(VP_RunnerK2);
                double adj = Math.Max(-0.2, Math.Min(0.2,
                    k1 * (lastQ_VP_Tailwind - 0.5) - k2 * (lastQ_VP_Headwind - 0.5)));

                double newPct = VPClamp01(lastRunnerPct + adj);
                // keep a base for telemetry but don’t destroy caller’s intent
                if (double.IsNaN(lastRunnerBasePct) || double.IsInfinity(lastRunnerBasePct) || lastRunnerBasePct == 0.0)
                    lastRunnerBasePct = lastRunnerPct;

                lastRunnerPct = newPct;
            }
