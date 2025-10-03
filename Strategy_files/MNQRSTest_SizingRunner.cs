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

        // Debug: print planned runner target and stop for visibility
        Print($"Runner planned target={plannedTargetPriceRunner:F2}, stop={plannedStopPriceRunner:F2}");

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

            // Determine exit type based on price proximity and special order name
            if (execution.Order.Name != null && execution.Order.Name.Equals("RunnerTimeout", StringComparison.OrdinalIgnoreCase))
            {
                exitType = "Timeout";
            }
            else if (Math.Abs(price - expectedTarget) < 0.5 * tick)
            {
                exitType = "Target";
            }
            else if (Math.Abs(price - expectedStop) < 0.5 * tick)
            {
                exitType = "Stop";
            }
            else if (Math.Abs(price - lastEntryPrice) < 0.5 * tick)
            {
                exitType = "Breakeven";
            }

            LogExit(side, entryName, price, exitType);

            // Log completed trade (entry + exit) and compute PnL metrics
            try
            {
                // Determine bars since entry for this signal
                int barsAgo = BarsSinceEntryExecution(0, entryName, 0);
                if (barsAgo >= 0)
                {
                    // Entry time from BarsAgo
                    DateTime entryTime = Times[0][barsAgo];
                    string entryDateString = entryTime.ToString("yyyy-MM-dd");
                    string entryTimeString = entryTime.ToString("HH:mm:ss");
                    string exitDateString = time.ToString("yyyy-MM-dd");
                    string exitTimeString = time.ToString("HH:mm:ss");
                    int holdBars = barsAgo;
                    double tickSize = Instrument?.MasterInstrument?.TickSize ?? 1.0;
                    // Calculate PnL per contract in price and ticks
                    double diff = (side == "Long") ? (price - lastEntryPrice) : (lastEntryPrice - price);
                    double pnlPrice = diff * quantity;
                    double pnlTicks = diff / tickSize;
                    // Risk is difference between entry and core target
                    double riskUnit = Math.Abs(plannedTargetPriceCore - lastEntryPrice);
                    double rMultiple = (riskUnit > 0) ? (diff / riskUnit) : 0.0;
                    // Capture current Q metrics and ATR
                    double atrValue = (atrIndicator != null) ? atrIndicator[0] : double.NaN;
                    double spaceR = lastQRes;
                    // Determine current regime
                    string regime = ActiveRegime;
                    // Log the completed trade
                    LogCompletedTrade(
                        entryDateString,
                        entryTimeString,
                        exitDateString,
                        exitTimeString,
                        side,
                        entryName,
                        Math.Abs(quantity),
                        lastEntryPrice,
                        price,
                        exitType,
                        holdBars,
                        pnlTicks,
                        pnlPrice,
                        rMultiple,
                        lastQTotal2,
                        lastQSwing,
                        lastQMomoRaw,
                        lastQVol,
                        lastQSession,
                        atrValue,
                        spaceR,
                        regime
                    );
                }
            }
            catch { }
        }

        /// <summary>
        /// Regime detection stub. Returns the currently active regime.  Future
        /// implementations can override this method to compute a dynamic regime
        /// based on market conditions.
        /// </summary>
        private string DetectRegime()
        {
            return ActiveRegime;
        }
    }
}