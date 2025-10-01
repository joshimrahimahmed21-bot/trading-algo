using System;


namespace QVP
{
    public enum TrailType
    {
        None,
        SR,
        ATR,
        SR_BE1R,
        SR_Extend,
        ATR_BE1R,
        Custom
    }

    public static class VPManagement
    {
        public static (TrailType trail, string label) ResolveTrailWithHysteresis(
            double congestion,
            double tightThreshHi = 0.55,
            double looseThreshLo = 0.50,
            TrailType last = TrailType.SR)
        {
            congestion = Clamp01(congestion);
            if (last == TrailType.ATR || last == TrailType.ATR_BE1R)
            {
                if (congestion >= tightThreshHi) return (TrailType.SR_BE1R, "SR+BE@1R");
                return (last, last == TrailType.ATR_BE1R ? "ATR+BE@1R" : "ATR");
            }
            else
            {
                if (congestion < looseThreshLo) return (TrailType.ATR_BE1R, "ATR+BE@1R");
                return (TrailType.SR, "SR");
            }
        }

        public static double RunnerBias(double baseRunnerPct, double tailwind, double headwind, double k1 = 0.10, double k2 = 0.10)
        {
            baseRunnerPct = Clamp01(baseRunnerPct);
            tailwind = Clamp01(tailwind);
            headwind = Clamp01(headwind);
            double adj = k1 * tailwind - k2 * headwind;
            return Clamp01(baseRunnerPct + adj);
        }

        public static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
    }

    // Minimal debounced regime classifier for presets
    public enum RunnerPresetKind { Default, StrictChop, TrendyRoomy }

    public sealed class RegimeClassifier
    {
        public double AtrHi = 1.25;
        public double AtrLo = 0.85;
        public double OverlapHi = 0.65;
        public double OverlapLo = 0.35;
        public int HoldBars = 10;
        public int MinBarsBetweenSwitches = 50;

        private RunnerPresetKind _current = RunnerPresetKind.Default;
        private RunnerPresetKind _pending = RunnerPresetKind.Default;
        private int _pendingCount = 0;
        private int _barsSinceSwitch = 999999;

        public RegimeClassifier() { }
        public RegimeClassifier(double atrHi, double atrLo, double overlapHi, double overlapLo, int holdBars, int minBarsBetween)
        {
            AtrHi = atrHi; AtrLo = atrLo; OverlapHi = overlapHi; OverlapLo = overlapLo;
            HoldBars = holdBars; MinBarsBetweenSwitches = minBarsBetween;
        }

        public void OnNewBar(bool atrHigh, bool atrLow, double overlapPct)
        {
            _barsSinceSwitch++;
            var want =
                atrHigh && overlapPct < OverlapLo ? RunnerPresetKind.TrendyRoomy :
                atrLow  && overlapPct > OverlapHi ? RunnerPresetKind.StrictChop :
                RunnerPresetKind.Default;

            if (want == _pending) _pendingCount++; else { _pending = want; _pendingCount = 1; }
            if (_pending == _current) return;
            if (_pendingCount >= HoldBars && _barsSinceSwitch >= MinBarsBetweenSwitches)
            {
                _current = _pending;
                _barsSinceSwitch = 0;
            }
        }

        public RunnerPresetKind CurrentKind => _current;

        public static string ToPresetName(RunnerPresetKind k)
        {
            switch (k)
            {
                case RunnerPresetKind.TrendyRoomy: return "TrendyRoomy";
                case RunnerPresetKind.StrictChop:  return "StrictChop";
                default:                            return "Default";
            }
        }
    }
}
