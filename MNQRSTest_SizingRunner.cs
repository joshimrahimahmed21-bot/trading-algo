using System;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using System.ComponentModel.DataAnnotations;

namespace NinjaTrader.NinjaScript.Strategies
    public partial class MNQRSTest : Strategy
    {
        private double plannedStopPrice = 0.0;
        private double triggerPrice = 0.0;
        // Runner split state
        private double lastRunnerPct;
        
        private void EnsureSplitSizingReady()
        {
            // Ensure runner percentage is adjusted based on current VP bias (if enabled)
            if (ApplyRunnerManagement && UseVolumeProfile)
            {
                double bias = tailwind - headwind;   // tailwind/headwind set in UpdateVPContext()
                double k = 0.1;
                double newRunnerPct = 0.5 + k * bias;
                // Cap adjustment to [0,1]
                if (newRunnerPct < 0.0) newRunnerPct = 0.0;
                if (newRunnerPct > 1.0) newRunnerPct = 1.0;
                lastRunnerPct = newRunnerPct;
            }
            else
            {
                // Static default split (50% runner if multiple contracts, or no runner if single contract)
                lastRunnerPct = (DefaultQuantity < 2 ? 0.0 : 0.5);
            }
        }
        
        private void ApplyRunnerPreset(bool isLong)
        {
            // Submit entry orders split into CORE and RUNNER portions based on lastRunnerPct
            int totalQty = (int)DefaultQuantity;
            if (totalQty <= 0) totalQty = 1;
            int runnerQty = (int)Math.Floor(totalQty * lastRunnerPct);
            if (runnerQty < 0) runnerQty = 0;
            if (runnerQty > totalQty) runnerQty = totalQty;
            int coreQty = totalQty - runnerQty;
            if (isLong)
            {
                if (coreQty > 0) EnterLong(coreQty, "CORE");
                if (runnerQty > 0) EnterLong(runnerQty, "RUNNER");
            }
            else
            {
                if (coreQty > 0) EnterShort(coreQty, "CORE");
                if (runnerQty > 0) EnterShort(runnerQty, "RUNNER");
            }
        }
    }
}

    public partial class MNQRSTest : Strategy
    {
        /// <summary>
        /// Determine the tick value in USD for the instrument (point value * tick size):contentReference[oaicite:128]{index=128}
        /// </summary>
        private double TickValueUSD()
        {
            try
            {
                return Instrument.MasterInstrument.PointValue * Instrument.MasterInstrument.TickSize;
            }
            catch (Exception ex) { Print("[AutoCatch] " + ex.Message); return 1.0; }
        }

        /// <summary>
        /// Round a quantity to the nearest lot size increment.
        /// </summary>
        private int RoundToLot(int qty)
        {
            int lot = Math.Max(1, RoundLot);
            int q = Math.Max(0, qty);
            int r = q % lot;
            int rounded = q - r;
            if (r >= (lot / 2)) rounded += lot;
            if (rounded < lot) rounded = lot;
            return rounded;
        }

/// <summary>
/// Resolve risk in ticks for current planned entry (ensuring at least 1 tick).
/// </summary>
private int ResolveRiskTicks()
{
    double tick = Math.Max(Instrument.MasterInstrument.TickSize, 1e-9);
    double riskTicksD;

    // Prefer the planned prices (set by EntrySignal before sizing)
    if (!double.IsNaN(plannedStopPrice) && !double.IsNaN(triggerPrice) && plannedStopPrice > 0 && triggerPrice > 0)
    {
        riskTicksD = Math.Abs(plannedStopPrice - triggerPrice) / tick;
    }
    else
    {
        // Fallback: use current bar's envelope as a conservative proxy
        double fallbackRisk = Math.Abs(High[0] - Low[0]) / tick;
        riskTicksD = Math.Max(1.0, fallbackRisk);
    }

    return Math.Max(1, (int)Math.Round(riskTicksD));
}

/// <summary>
/// Compute number of contracts to trade based on risk per trade and current risk in USD.
/// </summary>
        private int ComputeContractsForEntry(bool applySizeBias)
        {
            int riskTicks = ResolveRiskTicks();
            double tickUSD = Math.Max(0.01, TickValueUSD());
            double riskPerContract = riskTicks * tickUSD;
            int baseQty = (riskPerContract > 0.0 ? (int)Math.Floor(RiskPerTradeUSD / riskPerContract) : 1);
            baseQty = Math.Max(MinContracts, Math.Min(MaxContracts, baseQty));
            if (applySizeBias)
            {
                baseQty = (int)Math.Round(baseQty * Math.Max(0.25, lastSizeBias));
            }
            baseQty = Math.Max(MinContracts, Math.Min(MaxContracts, baseQty));
            // Round to nearest lot size
            int lot = Math.Max(1, RoundLot);
            baseQty = (int)(Math.Ceiling(baseQty / (double)lot) * lot);
            if (baseQty < 1) baseQty = 1;
            return baseQty;
        }

        /// <summary>
        /// Ensure risk-based split sizing is pre-computed (for lastCalcQty values).
        /// </summary>
        private void EnsureSplitSizingReady()
        {
            try
            {
                if (!ApplyRunnerManagement) return;
                // Compute risk-based entry quantity when BaseQty==0; else enforce min for runners:contentReference[oaicite:129]{index=129}:contentReference[oaicite:130]{index=130}
                int qtyEntry = 0;
                if (BaseQty <= 0)
                {
                    int riskTicks = 0;
                    try { riskTicks = ResolveRiskTicks(); } catch (Exception ex) { Print("[AutoCatch] " + ex.Message); }
                    if (riskTicks <= 0) riskTicks = 1;
                    double tickVal = TickValueUSD();
                    if (tickVal <= 0)
                        tickVal = (Instrument != null && Instrument.MasterInstrument != null)
                                  ? Instrument.MasterInstrument.PointValue * TickSize : 5.0;
                    double perContractRisk = riskTicks * tickVal;
                    if (perContractRisk <= 0) perContractRisk = tickVal;
                    qtyEntry = (int)Math.Floor(RiskPerTradeUSD / perContractRisk);
                }
                else
                {
                    qtyEntry = BaseQty;
                }
                // Respect min/max and ensure at least 2 if runner portion intended:contentReference[oaicite:131]{index=131}:contentReference[oaicite:132]{index=132}
                if (BaseQty <= 0 && lastRunnerPct > 0.0)
                    qtyEntry = Math.Max(Math.Max(2, MinContracts), qtyEntry);
                else
                    qtyEntry = Math.Max(MinContracts, qtyEntry);
                if (MaxContracts > 0) qtyEntry = Math.Min(MaxContracts, qtyEntry);
                if (qtyEntry < 1) qtyEntry = 1;
                LastCalcQty = (lastRunnerPct > 0.0 && qtyEntry < 2 ? 2 : qtyEntry);
                // Compute split preview (runner vs TP):contentReference[oaicite:133]{index=133}
                int qtyRunner = 0;
                if (lastRunnerPct > 0.0 && qtyEntry >= 2)
                {
                    qtyRunner = (int)Math.Ceiling(qtyEntry * lastRunnerPct);
                    if (qtyRunner >= qtyEntry) qtyRunner = qtyEntry - 1;
                    if (qtyRunner < 1) qtyRunner = 1;
                }
                LastCalcQtyRunner = qtyRunner;
                LastCalcQtyTP = Math.Max(0, qtyEntry - qtyRunner);
                // (No orders placed here; just preparing for reference):contentReference[oaicite:134]{index=134}
            }
            catch (Exception ex)
            {
                Print("[SplitSizing] " + ex.Message);
            }
        }
    }
}
    public partial class MNQRSTest : Strategy
    {
        private void ApplyVPManagementAdjustments()
        {
            // Toggleable trailing-stop or runner adjustments based on VP context.
            // **TODO:** Implement trail mode switching on congestion, etc.
            // Currently a placeholder (no additional trailing logic beyond base strategy behavior).
        }

    public partial class MNQRSTest : Strategy
    {
        // === Runner quality response curve knobs (canonical definitions) ===

        [NinjaScriptProperty, Display(Name = "QRes_CenterR", GroupName = "Runner/QRes", Order = 10)]
        public double QRes_CenterR { get; set; } = 1.2;   // center of S-curve in R

        [NinjaScriptProperty, Display(Name = "QRes_SmoothC", GroupName = "Runner/QRes", Order = 11)]
        public double QRes_SmoothC { get; set; } = 0.6;   // smoothness of curve

        [NinjaScriptProperty, Display(Name = "QRes_MinScale", GroupName = "Runner/QRes", Order = 12)]
        public double QRes_MinScale { get; set; } = 0.30; // minimum runner portion

        [NinjaScriptProperty, Display(Name = "QRes_LinA", GroupName = "Runner/QRes", Order = 13)]
        public double QRes_LinA { get; set; } = 0.25;     // linear alt: slope

        [NinjaScriptProperty, Display(Name = "QRes_LinB", GroupName = "Runner/QRes", Order = 14)]
        public double QRes_LinB { get; set; } = 0.50;     // linear alt: intercept
