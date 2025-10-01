namespace NinjaTrader.NinjaScript.Strategies
    public partial class MNQRSTest : Strategy
    {
        // Signal flags
        private bool entryLongSignal;
        private bool entryShortSignal;
        
        private void UpdateEntrySignals()
        {
            // TODO: Define actual entry logic.
            // For now, no explicit trade signals (both remain false).
            entryLongSignal = false;
            entryShortSignal = false;
        }
    }
}
    public partial class MNQRSTest : Strategy
    {
        protected override void OnBarUpdate()
        {
            if (BarsInProgress != 0) return;            // only process primary series
            if (CurrentBar < 1) return;                // need at least one prior bar for indicators
            
            // 1. Update core quality metrics
            ComputeBaseQualityMetrics();
            UpdatePosVolInputs();
            // Use directional context
            bool isLong = entryLongSignal || (Position.MarketPosition == MarketPosition.Long);
            MomentumCore_Update(isLong, out double qCore);
            lastQMomoCore = qCore;
            MomoFamilies_Update(Q_Space, Q_Trend);
            ComputeQualityScores();
            
            // 2. Update volume-profile context (if enabled)
            if (UseVolumeProfile)
                UpdateVPContext();
            
            // 3. Evaluate entry signals and apply quality gate
            UpdateEntrySignals();
            if (Position.MarketPosition == MarketPosition.Flat)      // only enter if flat
            {
                bool allowEntry = true;
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
            
            // 4. In-trade management (trailing stop / runner adjustments)
            if (Position.MarketPosition != MarketPosition.Flat)
            {
                ApplyVPManagementAdjustments();
            }
        }
    }
}
    public partial class MNQRSTest : Strategy
    {
        // Internal state for quality metrics
        private double Q_Space, Q_Trend, Q_Res;
        private double Q_Momo, PosMomo;
        // Last values of new signals
        private double lastQPosVolProxy, lastQPosVolProxyConf;
        private double lastQTotalOld, lastQTotalNew;
        
        // Utility objects for PosVol calculation
        private RollingStats deltaStats;
        private Ema dirEma;
        private Ema deltaEma;
        
        private void ComputeBaseQualityMetrics()
        {
            // Compute base context quality scores (space, trend, structural res, momentum).
            // (For now, these are placeholders. E.g., Q_Space/Q_Res=1 implies no spacing issue or strong structure by default.)
            Q_Space = 1.0;
            Q_Res = 1.0;
            // Trend quality: use ADX as proxy (normalize 0-100 to 0-1)
            double adxVal = adxIndicator != null ? adxIndicator[0] : 0.0;
            if (adxVal < 0) adxVal = 0;
            if (adxVal > 100) adxVal = 100;
            Q_Trend = adxVal / 100.0;
            // Momentum quality (legacy): use RSI as a simple momentum gauge
            double rsiVal = rsiIndicator != null ? rsiIndicator[0] : 50.0;
            if (rsiVal < 0) rsiVal = 0;
            if (rsiVal > 100) rsiVal = 100;
            Q_Momo = rsiVal / 100.0;
            PosMomo = Q_Momo;   // PosMomo could be directional momentum; here we use Q_Momo as a proxy.
        }
        
        private void UpdatePosVolInputs()
        {
            // Calculate Q_PosVol_Proxy (directional volume quality) each bar
            double volBuy, volSell;
            double totVol = Volume[0];
            if (totVol < 1e-9)
            {
                volBuy = volSell = 0.0;
                // If true bid/ask volume not available, approximate via bar direction
                int sign = Math.Sign(Close[0] - Open[0]);
                double buyFrac = (sign + 1.0) / 2.0;   // 1 for up-bar, 0 for down-bar, 0.5 for no change
                volBuy = buyFrac * totVol;
                volSell = totVol - volBuy;
            double total = volBuy + volSell + 1e-9;
            double buyPct = Clamp01(volBuy / total);
            // Determine directional volume bias relative to trade side (assume long context for now)
            double dirRaw = buyPct;
            double dirSmoothed = dirEma.Update(dirRaw);
            double delta = volBuy - volSell;
            double deltaSmoothed = deltaEma.Update(delta);
            double zDelta = deltaStats.UpdateAndZ(deltaSmoothed);
            double volStrength = Squash(zDelta);
            // Blend directional bias and strength
            double wDel = 0.4;
            double qDir = Blend(dirSmoothed, volStrength, wDel);
            double wSess = 0.15;
            double sessW = (UseSessionAnchor ? Session_WeightNow() : 1.0);
            double qPosVol = (1.0 - wSess) * qDir + wSess * (qDir * sessW);
            double Q_PosVol = Clamp01(qPosVol);
            lastQPosVolProxy = Q_PosVol;
            lastQPosVolProxyConf = 1.0;   // confidence placeholder (would decrease if volume nodes disagree)
        
        
        
        
        
        private void ComputeQualityScores()
        {
            // Compute old composite quality (average of existing factors for reference)
            double sumOld = Q_Space + Q_Trend + Q_Res;
            double QTotal_Old = (sumOld / 3.0);
            lastQTotalOld = Clamp01(QTotal_Old);
            // Compute new composite quality including new weighted factors
            double weightedSum = 0.0;
            double totalWeight = 0.0;
            // Add new factors if weights > 0
            if (W_PosVolProxy > 1e-6) { weightedSum += W_PosVolProxy * lastQPosVolProxy; totalWeight += W_PosVolProxy; }
            // removed W_PosVolVP term (signal not implemented)
            // removed W_PosRes term (signal not implemented)
            // Add legacy factors (each implicitly weight 1.0)
            weightedSum += (Q_Space + Q_Trend + Q_Res);
            totalWeight += 3.0;
            double QTotal_New = (totalWeight > 1e-6 ? weightedSum / totalWeight : 0.0);
            lastQTotalNew = Clamp01(QTotal_New);
}}
