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

        // Setup-arm state for breakout entries.  When a small candle crosses the EMA,
        // the strategy records the setup bar's high/low and direction.  On subsequent
        // bars, if price breaks beyond the setup range by one tick in the appropriate
        // direction, a stop-market order will be submitted.  These fields track the
        // armed state and parameters.
        private bool setupArmed;
        private bool setupIsLong;
        private double setupHigh;
        private double setupLow;
        private int setupBarsAgo;

        // Quality metric components for the current bar
        private double Q_Space;
        private double Q_Trend;
        private double Q_Res;
        private double Q_Momo;
        // Composite quality values
        private double lastQTotalOld;
        private double lastQTotalNew;

        // Helper functions for entry filters
        /// <summary>
        /// Determine if the current time is within the allowed entry window.  If the
        /// session filter is disabled the result is always true.
        /// </summary>
        private bool IsWithinEntryTime()
        {
            if (!UseEntryTimeFilter)
                return true;
            // Use the primary bar series time stamp (Times[0][0])
            DateTime ts = Times[0][0];
            int minutes = ts.Hour * 60 + ts.Minute;
            int startMinutes = EntryStartHour * 60 + EntryStartMinute;
            int endMinutes = EntryEndHour * 60 + EntryEndMinute;
            if (startMinutes <= endMinutes)
            {
                return minutes >= startMinutes && minutes <= endMinutes;
            }
            else
            {
                // Over midnight wrap
                return (minutes >= startMinutes) || (minutes <= endMinutes);
            }
        }

        /// <summary>
        /// Determine if the ATR value is within the configured volatility bounds.  If the
        /// volatility filter is disabled the result is always true.
        /// </summary>
        private bool IsVolatilityOk()
        {
            if (!UseVolatilityFilter)
                return true;
            // Ensure ATR indicator exists
            double atrVal = atrIndicator != null ? atrIndicator[0] : 0.0;
            return atrVal >= MinATR && atrVal <= MaxATR;
        }

        /// <summary>
        /// Determine if the EMA slope meets the minimum threshold in the direction of the
        /// trade.  If the trend filter is disabled the result is always true.
        /// </summary>
        /// <param name="wantLong">True if evaluating a long setup; false for short.</param>
        private bool IsTrendOk(bool wantLong)
        {
            if (!UseTrendFilter)
                return true;
            // Compute raw slope (difference between current and prior EMA values)
            if (priceEma == null || CurrentBar < 1)
                return false;
            double slope = priceEma[0] - priceEma[1];
            if (wantLong)
            {
                return slope >= TrendSlopeMin;
            }
            else
            {
                return (-slope) >= TrendSlopeMin;
            }
        }

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
    // EMA + small candle breakout entry logic
    // Reset direct entry flags (not used for breakout entries but retained for compatibility)
    entryLongSignal = false;
    entryShortSignal = false;

    // Ensure we have enough bars and the EMA is initialized
    if (CurrentBar < 1 || priceEma == null)
        return;

    // Compute bar range and small range threshold (in price units)
    double barRange = High[0] - Low[0];
    double threshold = Instrument.MasterInstrument.TickSize * MinAbsSpaceTicks;
    // Debug: skip bars with too large a range for breakout setups
    if (barRange > threshold)
    {
        Print($"Skip: Bar too large (range {barRange:F2} > smallThreshold {threshold:F2})");
        return;
    }

    // Determine EMA crossing relative to previous bar and evaluate touch-and-bounce conditions
    bool crossUp = (Close[1] <= priceEma[1]) && (Close[0] > priceEma[0]);
    bool crossDown = (Close[1] >= priceEma[1]) && (Close[0] < priceEma[0]);

    // Additional touch-and-bounce evaluation around the EMA.  Price may touch the EMA within a small threshold
    // and close at least that distance beyond the EMA to count as an entry interaction.
    {
        double emaNow = priceEma[0];
        double tickSize = Instrument.MasterInstrument.TickSize;
        double touchThresh = tickSize * MinEmaTouchTicks;
        if (!crossUp)
        {
            if ((Low[0] <= emaNow) && ((emaNow - Low[0]) <= touchThresh) && ((Close[0] - emaNow) >= touchThresh))
            {
                crossUp = true;
            }
        }
        if (!crossDown)
        {
            if ((High[0] >= emaNow) && ((High[0] - emaNow) <= touchThresh) && ((emaNow - Close[0]) >= touchThresh))
            {
                crossDown = true;
            }
        }
    }

    // If neither a long nor a short EMA interaction is detected, skip setup and log the reason
    if (!crossUp && !crossDown)
    {
        Print("Skip: No EMA cross or touch detected");
        LogSetupRow("Skip", "", "NoEmaTouch", High[0], Low[0], 0, 0, 0);
        return;
    }

    // Only consider crosses on small bars
    if (barRange <= threshold)
    {
        bool allowSetup = true;

        // Quality gate
        if (UseQualityGate && lastQTotalNew < MinQTotal2)
        {
            Print($"Skip: QualityGate (QTotalNew {lastQTotalNew:F2} < MinQTotal2 {MinQTotal2:F2})");
            LogSetupRow("Skip", crossUp ? "Long" : "Short", "QualityFail", High[0], Low[0], 0, 0, 0);
            allowSetup = false;
        }
        // Session time filter
        if (!IsWithinEntryTime())
        {
            Print("Skip: Time filter fail");
            LogSetupRow("Skip", crossUp ? "Long" : "Short", "TimeFail", High[0], Low[0], 0, 0, 0);
            allowSetup = false;
        }
        // Volatility filter
        if (!IsVolatilityOk())
        {
            Print("Skip: Volatility filter fail");
            LogSetupRow("Skip", crossUp ? "Long" : "Short", "VolFail", High[0], Low[0], 0, 0, 0);
            allowSetup = false;
        }
        // Trend filter
        if (crossUp && !IsTrendOk(true))
        {
            Print("Skip: Trend filter fail (long)");
            LogSetupRow("Skip", "Long", "TrendFail", High[0], Low[0], 0, 0, 0);
            allowSetup = false;
        }
        if (crossDown && !IsTrendOk(false))
        {
            Print("Skip: Trend filter fail (short)");
            LogSetupRow("Skip", "Short", "TrendFail", High[0], Low[0], 0, 0, 0);
            allowSetup = false;
        }

        if (allowSetup)
        {
            if (crossUp)
            {
                // --- Pass 10: space/resistance gating ---
                if (!CheckSpaceAndResistance(true))
                {
                    Print("Skip: Space/resistance fail (long)");
                    LogSetupRow("Skip", "Long", "SpaceFail", High[0], Low[0], 0, 0, 0);
                    return;
                }

                // Arm long breakout setup
                setupArmed = true;
                setupIsLong = true;
                setupHigh = High[0];
                setupLow = Low[0];
                setupBarsAgo = 0;
                Print($"Armed: Long setup at bar {CurrentBar}, high={setupHigh:F2}, low={setupLow:F2}");
                LogSetupRow("Armed", "Long", "", setupHigh, setupLow, 0, 0, 0);
            }
            else if (crossDown)
            {
                if (!CheckSpaceAndResistance(false))
                {
                    Print("Skip: Space/resistance fail (short)");
                    LogSetupRow("Skip", "Short", "SpaceFail", High[0], Low[0], 0, 0, 0);
                    return;
                }

                // Arm short breakout setup
                setupArmed = true;
                setupIsLong = false;
                setupHigh = High[0];
                setupLow = Low[0];
                setupBarsAgo = 0;
                Print($"Armed: Short setup at bar {CurrentBar}, high={setupHigh:F2}, low={setupLow:F2}");
                LogSetupRow("Armed", "Short", "", setupHigh, setupLow, 0, 0, 0);
            }
        }
    }
}


/// <summary>
/// Main strategy logic executed on each new bar.  This method
/// computes quality metrics, updates volume profile context,
/// checks quality gates and triggers entries via the runner
/// preset functions.  In-trade management adjustments are
/// deferred to ApplyVPManagementAdjustments().
/// </summary>
protected override void OnBarUpdate()
{
    if (BarsInProgress != 0) return;
    // Require at least one completed bar before processing
    if (CurrentBar < 1) return;

    // Force-entry debug mode: if enabled and flat, submit a trade each bar
    if (ForceEntry && Position.MarketPosition == MarketPosition.Flat)
    {
        bool forceIsLong = Close[0] > Open[0];    // decide direction by bar direction
        EnsureSplitSizingReady();
        ApplyRunnerPreset(forceIsLong);
        return;   // skip normal logic
    }
    // Safety: make sure indicators are initialized and enough bars are available
    if (priceEma == null || atrIndicator == null)
    {
        // Print a heartbeat so we know why processing is skipped
        Print($"InitGuard: indicators not ready (EMA or ATR is null) at bar {CurrentBar}");
        return;
    }
    // Warmup guard: wait until enough bars exist to compute EMA/ATR before proceeding
    int warmupBarsNeeded = Math.Max(EntryEmaPeriod, 14);
    if (CurrentBar < warmupBarsNeeded)
    {
        // Print every 10 bars to avoid log spam
        if (CurrentBar % 10 == 0)
            Print($"Warmup: waiting for {warmupBarsNeeded} bars, currently {CurrentBar}");
        return;
    }


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

    // === Quality metrics expansion (Pass 9) ===
    // Q_Swing: slope of the price EMA (scaled by tick size)
    double emaSlope = (CurrentBar >= 1) ? (priceEma[0] - priceEma[1]) : 0.0;
    lastQSwing = Helpers.Clamp01(Math.Abs(emaSlope) / (Instrument.MasterInstrument.TickSize * 10.0));

    // Q_Momo: use the core momentum measure
    lastQMomoRaw = lastQMomoCore;

    // Q_Vol: normalised ATR
    double atrNorm = (atrIndicator != null) ? atrIndicator[0] / (MaxATR > 0 ? MaxATR : 1.0) : 0.0;
    lastQVol = Helpers.Clamp01(atrNorm);

    // Q_Session: session time weighting
    lastQSession = Session_WeightNow();

    // 5. Update volume profile context if enabled
    if (UseVolumeProfile)
    {
        UpdateVPContext();
    }
    // 6. Determine entry signals or arm breakout setups
    UpdateEntrySignals();
    // Handle armed breakout setups when flat
    if (Position.MarketPosition == MarketPosition.Flat)
    {
        // If a setup is currently armed, look for the breakout trigger
        if (setupArmed)
        {
            // Compute the breakout price (one tick beyond the setup bar)
            double tick = Instrument.MasterInstrument.TickSize;
            if (setupIsLong)
            {
                double breakoutPrice = setupHigh + tick;
                // If price trades above breakout level on this bar, submit a stop-market order
                if (High[0] > breakoutPrice)
                {
                    // breakout trigger detected: fire trade and log triggered setup
                    EnsureSplitSizingReady();
                    ApplyRunnerPreset(true);
                    // log the triggered event with breakout price and planned core target/stop for reference
                    try
                    {
                        LogSetupRow("Triggered", "Long", "", breakoutPrice, plannedStopPriceCore, plannedTargetPriceCore, lastQRes, 0);
                    }
                    catch { /* ignore if logging fails */ }
                    setupArmed = false;
                }
                else
                {
                    setupBarsAgo++;
                    if (setupBarsAgo > 3)
                        setupArmed = false;
                }
            }
            else
            {
                double breakoutPrice = setupLow - tick;
                if (Low[0] < breakoutPrice)
                {
                    // breakout trigger detected: fire trade and log triggered setup
                    EnsureSplitSizingReady();
                    ApplyRunnerPreset(false);
                    try
                    {
                        LogSetupRow("Triggered", "Short", "", breakoutPrice, plannedStopPriceCore, plannedTargetPriceCore, lastQRes, 0);
                    }
                    catch { /* ignore if logging fails */ }
                    setupArmed = false;
                }
                else
                {
                    setupBarsAgo++;
                    if (setupBarsAgo > 3)
                        setupArmed = false;
                }
				if (setupArmed && setupBarsAgo > 3) {
    LogSetupRow("Expired", setupIsLong ? "Long":"Short", "Stale", setupHigh, setupLow, 0,0,0);
    setupArmed = false;
}

            }
        }
        // If no setup is armed, we could consider other direct entry signals (if any)
    }
    // 7. In-trade management
    if (Position.MarketPosition != MarketPosition.Flat)
    {
        ApplyVPManagementAdjustments();
    }
}
   private bool CheckSpaceAndResistance(bool isLong)
{
    int lookback = 20;
    double localMaxHigh = High[0];
    double localMinLow = Low[0];
    for (int i = 1; i <= Math.Min(CurrentBar, lookback); i++)
    {
        if (High[i] > localMaxHigh) localMaxHigh = High[i];
        if (Low[i] < localMinLow)  localMinLow  = Low[i];
    }

    double barRange = High[0] - Low[0];
    double minRisk  = Instrument.MasterInstrument.TickSize * Math.Max(1, MinAbsSpaceTicks);
    double riskR    = Math.Max(barRange, minRisk);

    double spaceR = isLong
        ? (localMaxHigh - Close[0]) / riskR
        : (Close[0] - localMinLow) / riskR;

    lastQRes = spaceR;
    double qResRunner = Helpers.Clamp01(spaceR / 2.0); // ≥2R = 1.0

    if (spaceR < MinSpaceR)
        return false;

    return true;
}
 }
}
