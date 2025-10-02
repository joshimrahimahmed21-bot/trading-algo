using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public enum SessionCentersMode { Off = 0, FixedAnchor = 1, Adaptive = 2 }

    public partial class MNQRSTest : Strategy
    {
        [NinjaScriptProperty]
        [Display(Name = "UseSessionAnchor", GroupName = "Session", Order = 10)]
        public bool UseSessionAnchor { get; set; }

        [NinjaScriptProperty][Range(0, 23)]
        [Display(Name = "AnchorHour", GroupName = "Session", Order = 11)]
        public int AnchorHour { get; set; }

        [NinjaScriptProperty][Range(0, 59)]
        [Display(Name = "AnchorMinute", GroupName = "Session", Order = 12)]
        public int AnchorMinute { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "SessionWindowShape", GroupName = "Session", Order = 13)]
        public string SessionWindowShape { get; set; }

        [NinjaScriptProperty][Range(0.0, 10.0)]
        [Display(Name = "PreScale", GroupName = "Session", Order = 14)]
        public double PreScale { get; set; }

        [NinjaScriptProperty][Range(0.0, 10.0)]
        [Display(Name = "PostScale", GroupName = "Session", Order = 15)]
        public double PostScale { get; set; }

        [NinjaScriptProperty][Range(1.0, 1440.0)]
        [Display(Name = "AnchorWindowMins", GroupName = "Session", Order = 16)]
        public double AnchorWindowMins { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "SessionCentersMode", GroupName = "Session", Order = 17)]
        public SessionCentersMode SessionCentersModeParamV2 { get; set; }

        private DateTime? sessionAnchorUtc;

        private double Session_WeightNow()
        {
            if (!UseSessionAnchor || SessionCentersModeParamV2 == SessionCentersMode.Off)
                return 1.0;

            if (!sessionAnchorUtc.HasValue)
                sessionAnchorUtc = new DateTime(Times[0][0].Year, Times[0][0].Month, Times[0][0].Day, AnchorHour, AnchorMinute, 0);

            double deltaMins = Math.Abs((Times[0][0] - sessionAnchorUtc.Value).TotalMinutes);
            double window = Math.Max(1.0, AnchorWindowMins);

            double core;
            if (SessionWindowShape.ToLower().StartsWith("tri"))
                core = Math.Max(0.0, 1.0 - (deltaMins / window));
            else if (SessionWindowShape.ToLower() == "box")
                core = deltaMins <= window ? 1.0 : 0.0;
            else
            {
                double sigma = window / 2.0;
                core = Math.Exp(-(deltaMins * deltaMins) / (2.0 * sigma * sigma));
            }

            double w = core;
            w *= (PreScale > 0 ? PreScale : 1.0);
            w = Math.Min(1.0, Math.Max(0.0, w));
            w *= (PostScale > 0 ? PostScale : 1.0);
            return Math.Min(1.0, Math.Max(0.0, w));
        }

        private void UpdateVPContext()
        {
            lastQ_VP_Tailwind = 0.0;
            lastQ_VP_Headwind = 0.0;
            lastQ_VP_Cushion = 0.0;
            lastVP_Congestion = 0.0;
        }

        private void ApplyVPManagementAdjustments()
        {
            if (UseVPRunnerScaling && UseVolumeProfile)
            {
                double bias = lastQ_VP_Tailwind - lastQ_VP_Headwind;
                double adj = Math.Max(-0.2, Math.Min(0.2, bias * 0.1));
                lastRunnerPct = Helpers.Clamp01(lastRunnerPct + adj);
            }
        }
    }
}
