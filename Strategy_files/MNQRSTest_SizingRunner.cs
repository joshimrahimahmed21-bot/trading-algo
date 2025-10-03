using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Runner management for MNQRSTest with eligibility gating and exit logging.
    /// CORE closes at +1R, RUNNER stop sits at breakeven. All exits logged.
    /// </summary>
    public partial class MNQRSTest : Strategy
    {
        private double lastEntryPrice;
        private double plannedTargetPriceCore, plannedStopPriceCore;
        private double plannedTargetPriceRunner, plannedStopPriceRunner;

        private void EnsureSplitSizingReady()
        {
            if (!ApplyRunnerManagement)
            {
                lastRunnerBasePct = 0.0;
                lastRunnerPct = 0.0;
                return;
            }
            lastRunnerBasePct = (BaseContracts < 2 ? 0.0 : 0.5);
            lastRunnerPct = lastRunnerBasePct;
        }

private void ApplyRunnerPreset(bool isLong)
{
    // compute normal allowRunner chain
    bool allowRunner = ApplyRunnerManagement && BaseContracts >= 2;
    if (allowRunner)
    {
        allowRunner = (lastQRes >= RunnerSpaceThreshold) && (lastQMomoCore >= RunnerMomoThreshold);
    }

    // debug: print each factor so you see what's blocking
    Print($"ApplyRunnerManagement={ApplyRunnerManagement}, BaseContracts={BaseContracts}, " +
          $"QRes={lastQRes:F2} vs {RunnerSpaceThreshold}, QMomo={lastQMomoCore:F2} vs {RunnerMomoThreshold}, " +
          $"Result={allowRunner}");

    // override for smoke-test:
    if (ForceEntry) allowRunner = true;


    int qty = Math.Max(1, BaseContracts);
    double entryPrice = Close[0];
    lastEntryPrice    = entryPrice;

    double barRange = High[0] - Low[0];
    double minRisk  = Instrument.MasterInstrument.TickSize * Math.Max(1, MinAbsSpaceTicks);
    double risk     = Math.Max(barRange, minRisk);

    double stopPrice, targetPrice;
    if (isLong)
    {
        stopPrice   = entryPrice - risk;
        targetPrice = entryPrice + risk;
    }
    else
    {
        stopPrice   = entryPrice + risk;
        targetPrice = entryPrice - risk;
    }
    plannedStopPrice   = stopPrice; // legacy single
    plannedTargetPrice = targetPrice; // legacy single

    plannedStopPriceCore = stopPrice;
    plannedTargetPriceCore = targetPrice;
    plannedStopPriceRunner = isLong ? entryPrice : entryPrice;
    // plannedTargetPriceRunner set below when allowRunner branch executes

    if (allowRunner)
    {
        int runnerQty = Math.Max(1, BaseContracts / 2);
        int coreQty   = Math.Max(1, BaseContracts - runnerQty);

        
// Compute runner target multiple (R) from lastRunnerPct; inverse mapping with clamps
double pct = Helpers.Clamp01(lastRunnerPct);
pct = Math.Max(0.1, Math.Min(0.9, pct));
double runnerR = Math.Max(1.5, Math.Min(6.0, 1.0 / pct));
double runnerTarget = isLong ? (entryPrice + runnerR * risk) : (entryPrice - runnerR * risk);
plannedTargetPriceRunner = runnerTarget;
plannedStopPriceRunner = entryPrice;

        // CORE: 1R stop + 1R target
        SetStopLoss     ("CORE",   CalculationMode.Price, stopPrice,  false);
        SetProfitTarget ("CORE",   CalculationMode.Price, targetPrice);

        // RUNNER: breakeven stop
        SetStopLoss     ("RUNNER", CalculationMode.Price, entryPrice, false);
        SetProfitTarget ("RUNNER", CalculationMode.Price, plannedTargetPriceRunner);

        if (coreQty > 0)
        {
            if (isLong) EnterLong(coreQty, "CORE");
            else        EnterShort(coreQty, "CORE");
            LogTradeRow(isLong ? "Long" : "Short", entryPrice, stopPrice, targetPrice, coreQty, "CORE");
        }
        if (runnerQty > 0)
        {
            if (isLong) EnterLong(runnerQty, "RUNNER");
            else        EnterShort(runnerQty, "RUNNER");
            LogTradeRow(isLong ? "Long" : "Short", entryPrice, entryPrice, plannedTargetPriceRunner, runnerQty, "RUNNER");
        }
    }
    else
    {
        // Single: full size, 1R stop + target
        SetStopLoss     ("Single", CalculationMode.Price, stopPrice,  false);
        SetProfitTarget ("Single", CalculationMode.Price, targetPrice);

        if (isLong)  EnterLong (qty, "Single");
        else         EnterShort(qty, "Single");
        LogTradeRow(isLong ? "Long" : "Short", entryPrice, stopPrice, targetPrice, qty, "Single");
    }
}



        /// <summary>
        /// Correct NT8 signature for execution updates: log exits using LogExit.
        /// </summary>
        protected override void OnExecutionUpdate(Execution execution, string executionId, double price,
            int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (execution == null || execution.Order == null) return;
            if (execution.Order.OrderState != OrderState.Filled) return;

            string entryName = execution.Order.FromEntrySignal ?? "";
            if (entryName != "CORE" && entryName != "RUNNER" && entryName != "Single")
                return;

            // Exit orders: generated by SetStopLoss/SetProfitTarget
            bool isExitFill = !string.Equals(execution.Order.Name ?? "", entryName, StringComparison.OrdinalIgnoreCase);
            if (!isExitFill) return;

            string side = (execution.Order.OrderAction == OrderAction.Sell) ? "Long"
                       : (execution.Order.OrderAction == OrderAction.BuyToCover) ? "Short"
                       : "?";

            double tick = Instrument?.MasterInstrument?.TickSize ?? 1.0;
            string exitType = "Manual";

            double expectedTarget = entryName == "CORE" ? plannedTargetPriceCore
                                   : entryName == "RUNNER" ? plannedTargetPriceRunner
                                   : plannedTargetPrice;
            double expectedStop   = entryName == "CORE" ? plannedStopPriceCore
                                   : entryName == "RUNNER" ? plannedStopPriceRunner
                                   : plannedStopPrice;

            if (Math.Abs(price - expectedTarget) < 0.5 * tick)
                exitType = "Target";
            else if (Math.Abs(price - expectedStop) < 0.5 * tick)
                exitType = "Stop";
            else if (Math.Abs(price - lastEntryPrice) < 0.5 * tick)
                exitType = "Breakeven";

            LogExit(side, entryName, price, exitType);
        }
    }
}
