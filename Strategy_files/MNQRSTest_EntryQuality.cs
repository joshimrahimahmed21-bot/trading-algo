using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Entry quality and signal management for the MNQRSTest strategy.  This
    /// partial contains the primary OnBarUpdate override along with helper
    /// methods to compute context quality scores (space, trend, resistance,
    /// momentum), update positional volume proxies and assemble a composite
    /// Q_Total2 score.  It also manages placeholder entry signals and
    /// applies the quality gate and runner preset logic.  The actual
    /// trade entry logic should be implemented in UpdateEntrySignals().
    /// </summary>
    public partial class MNQRSTest : Strategy
    {
        // Entry signal flags (arm long/short)
        private bool entryLongSignal;
        private bool entryShortSignal;

        // Quality metric components for the current bar
        private double Q_Space;
        private double Q_Trend;
        private double Q_Res;
        private double Q_Momo;
        // Composite quality values
        private double lastQTotalOld;
        private double lastQTotalNew;

        /// <summary>
        /// Compute the base quality scores for the current bar.  This
        /// includes space (always neutral in this baseline), trend and
        /// momentum.  ADX is used as a proxy for trend strength and RSI
        /// (normalized) as a proxy for raw momentum.  Resistance quality
        /// defaults to 1.0 in this simplified version.
        /// </summary>
        private void ComputeBaseQualityMetrics()
        {
            // Space and structural resistance quality are neutral in this stub
            Q_Space = 1.0;
            Q_Res = 1.0;
            // Trend quality: map ADX (0–100) to 0–1
            double adxVal = adxIndicator != null ? adxIndicator[0] : 0.0;
            adxVal = Math.Max(0.0, Math.Min(100.0, adxVal));
            Q_Trend = adxVal / 100.0;
            // Raw momentum quality: map RSI (0–100) to 0–1
            double rsiVal = rsiIndicator != null ? rsiIndicator[0] : 50.0;
            rsiVal = Math.Max(0.0, Math.Min(100.0, rsiVal));
            Q_Momo = rsiVal / 100.0;
        }

        /// <summary>
        /// Update the positional volume proxy (Q_PosVol_Proxy) using simple
        /// directional volume heuristics.  This implementation uses a
        /// smoothed volume imbalance and directional strength to derive
        /// a value in [0,1].  Session weighting is applied if a session
        /// anchor is enabled.  The resulting proxy and confidence are
        /// stored in lastQ_PosVol_Proxy and lastQ_PosVol_Proxy_Conf.
        /// </summary>
        private void UpdatePosVolInputs()
        {
            double totVol = (Volume != null && Volume.Count > 0) ? Volume[0] : 0.0;
            double volBuy = 0.0;
            double volSell = 0.0;
            if (totVol > 0.0)
            {
                // Estimate directional volume using bar direction
                int sign = Math.Sign(Close[0] - Open[0]);
                double buyFrac = (sign + 1.0) / 2.0; // 0 for down bar, 1 for up bar
                volBuy = buyFrac * totVol;
                volSell = totVol - volBuy;
            }
            double total = volBuy + volSell + 1e-9;
            double buyPct = Helpers.Clamp01(volBuy / total);
            // Smooth directional bias and delta using EMAs
            double dirSmoothed = dirEma != null ? dirEma.Update(buyPct) : buyPct;
            double delta = volBuy - volSell;
            double deltaSmoothed = deltaEma != null ? deltaEma.Update(delta) : delta;
            double zDelta = deltaStats != null ? deltaStats.UpdateAndZ(deltaSmoothed) : 0.0;
            double volStrength = Helpers.Squash(zDelta);
            // Blend smoothed direction and strength
            double qDir = Helpers.Blend(dirSmoothed, volStrength, 0.4);
            // Apply session weighting if enabled
            double sessWeight = UseSessionAnchor ? Session_WeightNow() : 1.0;
            double qPosVol = (1.0 - 0.15) * qDir + 0.15 * (qDir * sessWeight);
            double qp = Helpers.Clamp01(qPosVol);
            lastQ_PosVol_Proxy = qp;
            lastQ_PosVol_Proxy_Conf = 1.0;
        }

        /// <summary>
        /// Combine the individual quality components into composite old and
        /// new quality scores.  The old score is a simple average of
        /// space, trend and resistance quality.  The new score blends
        /// existing factors with the new PosVol proxy according to the
        /// configured weight W_PosVolProxy.  Additional factors may be
        /// added in future versions.
        /// </summary>
        private void ComputeQualityScores()
        {
            // Old composite quality: average of legacy factors
            double qOld = (Q_Space + Q_Trend + Q_Res) / 3.0;
            lastQTotalOld = Helpers.Clamp01(qOld);
            // New composite quality: weighted PosVol plus legacy factors
            double weightedSum = 0.0;
            double totalWeight = 0.0;
            if (W_PosVolProxy > 1e-6)
            {
                weightedSum += W_PosVolProxy * lastQ_PosVol_Proxy;
                totalWeight += W_PosVolProxy;
            }
            weightedSum += (Q_Space + Q_Trend + Q_Res);
            totalWeight += 3.0;
            double qNew = (totalWeight > 1e-6 ? weightedSum / totalWeight : 0.0);
            lastQTotalNew = Helpers.Clamp01(qNew);
        }

        /// <summary>
        /// Update entry signals.  In this compile‑safe stub the entry
        /// signals remain false.  Users should provide their own logic
        /// here (for example, EMA crossover and small candle breakout) to
        /// set entryLongSignal or entryShortSignal accordingly.
        /// </summary>
        private void UpdateEntrySignals()
        {
            entryLongSignal = false;
            entryShortSignal = false;
        }

        /// <summary>
        /// Main strategy logic executed on each new bar.  This method
        /// computes quality metrics, updates volume profile context,
        /// checks quality gates and triggers entries via the runner
        /// preset functions.  In‑trade management adjustments are
        /// deferred to ApplyVPManagementAdjustments().
        /// </summary>
        protected override void OnBarUpdate()
        {
            // Process only primary series and after enough bars are present
            if (BarsInProgress != 0) return;
            if (CurrentBar < 1) return;

            // 1. Compute base context quality metrics
            ComputeBaseQualityMetrics();
            // 2. Update positional volume inputs
            UpdatePosVolInputs();
            // 3. Update momentum core and families
            bool isLong = entryLongSignal || (Position.MarketPosition == MarketPosition.Long);
            MomentumCore_Update(isLong, out double qCore);
            lastQMomoCore = qCore;
            MomoFamilies_Update(Q_Space, Q_Trend);
            // 4. Compute composite quality scores
            ComputeQualityScores();
            // 5. Update volume profile context if enabled
            if (UseVolumeProfile)
            {
                UpdateVPContext();
            }
            // 6. Determine entry signals
            UpdateEntrySignals();
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                bool allowEntry = true;
                // Apply quality gate if enabled
                if (UseQualityGate && lastQTotalNew < MinQTotal2)
                    allowEntry = false;
                if (allowEntry)
                {
                    if (entryLongSignal)
                    {
                        EnsureSplitSizingReady();
                        ApplyRunnerPreset(true);
                    }
                    else if (entryShortSignal)
                    {
                        EnsureSplitSizingReady();
                        ApplyRunnerPreset(false);
                    }
                }
            }
            // 7. In‑trade management
            if (Position.MarketPosition != MarketPosition.Flat)
            {
                ApplyVPManagementAdjustments();
            }
        }
    }
}
