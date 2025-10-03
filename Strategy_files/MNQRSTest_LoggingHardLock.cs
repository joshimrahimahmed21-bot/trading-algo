using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class MNQRSTest : Strategy
    {
        private string tradeLogPath;
        private string exitLogPath;
        private string runInfoPath;
        private string setupLogPath;

        // Paths for completed trade and equity curve logs
        private string completedTradesPath;
        private string equityCurvePath;
        // Cumulative PnL for equity curve logging
        private double cumulativePnL;

        private void EnsureLogsInitialized()
        {
            if (logsReady) return;

            string docPath   = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string basePath  = System.IO.Path.Combine(docPath, "NinjaTrader 8", "MNQRSTestLogs");
            runStamp         = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string runFolder = System.IO.Path.Combine(basePath, runStamp);
            System.IO.Directory.CreateDirectory(runFolder);

            tradeLogPath = System.IO.Path.Combine(runFolder, "Trades.csv");
            setupLogPath = System.IO.Path.Combine(runFolder, "Setups.csv");
            exitLogPath  = System.IO.Path.Combine(runFolder, "Exits.csv");
            runInfoPath  = System.IO.Path.Combine(runFolder, "RunInfo.txt");

            // Completed trades and equity curve paths
            completedTradesPath = System.IO.Path.Combine(runFolder, "CompletedTrades.csv");
            equityCurvePath    = System.IO.Path.Combine(runFolder, "EquityCurve.csv");

            // Trades header: include contracts + runner info + regime
            System.IO.File.WriteAllText(
                tradeLogPath,
                "Date,Time,Side,Entry,Stop,Target,Qty,Contracts,EntryName,Q_Total2,Q_Swing,Q_Momo,Q_Vol,Q_Session,Q_ResRunner,SpaceR,ATR,SessionWeight,RunnerPct,RunnerR,Regime\n"
            );

            // Setups header
            System.IO.File.WriteAllText(
                setupLogPath,
                "Date,Time,Event,Side,Reason,Trig,Stop,Tgt,R,StopTicks,Notes,Regime\n"
            );

            // Exits header
            System.IO.File.WriteAllText(
                exitLogPath,
                "Date,Time,Side,EntryName,ExitPrice,ExitType\n"
            );

            // Completed trades header
            System.IO.File.WriteAllText(
                completedTradesPath,
                "Date,EntryTime,ExitTime,Side,EntryName,Qty,EntryPrice,ExitPrice,ExitType,HoldBars,PnL_Ticks,PnL_Price,R_Multiple,Q_Total2,Q_Swing,Q_Momo,Q_Vol,Q_Session,ATR,SpaceR,Regime\n"
            );

            // Initialize equity curve file if toggled
            if (LogEquityCurve)
            {
                System.IO.File.WriteAllText(
                    equityCurvePath,
                    "Date,Time,CumulativePnL\n"
                );
                cumulativePnL = 0.0;
            }

            // RunInfo.txt content
            string info = "";
            info += $"RunStamp: {runStamp}\n";
            info += $"BatchTag: {BatchTag}\n";
            info += $"MinQTotal2: {MinQTotal2}\n";
            info += $"UseQualityGate: {UseQualityGate}\n";
            info += $"ApplyRunnerManagement: {ApplyRunnerManagement}\n";
            info += $"UseVolumeProfile: {UseVolumeProfile}\n";
            info += $"UseEntryTimeFilter: {UseEntryTimeFilter}\n";
            info += $"EntryStartHour: {EntryStartHour}:{EntryStartMinute}\n";
            info += $"EntryEndHour: {EntryEndHour}:{EntryEndMinute}\n";
            info += $"UseVolatilityFilter: {UseVolatilityFilter}\n";
            info += $"MinATR: {MinATR}\n";
            info += $"MaxATR: {MaxATR}\n";
            info += $"UseTrendFilter: {UseTrendFilter}\n";
            info += $"TrendSlopeMin: {TrendSlopeMin}\n";
            info += $"MinSpaceR: {MinSpaceR}\n";
            info += $"MinAbsSpaceTicks: {MinAbsSpaceTicks}\n";
            info += $"BaseContracts: {BaseContracts}\n";
            info += $"EntryEmaPeriod: {EntryEmaPeriod}\n";
            info += $"MinEmaTouchTicks: {MinEmaTouchTicks}\n";
            info += $"UseMomentumCore: {UseMomentumCore}\n";
            info += $"UsePosVolNodes: {UsePosVolNodes}\n";
            info += $"W_QSwing: {W_QSwing}\n";
            info += $"W_QMomo: {W_QMomo}\n";
            info += $"W_QVol: {W_QVol}\n";
            info += $"W_QSession: {W_QSession}\n";
            info += $"W_PosVol: {W_PosVolProxy}\n";
            info += $"RunnerMomoThreshold: {RunnerMomoThreshold}\n";
            info += $"RunnerSpaceThreshold: {RunnerSpaceThreshold}\n";

            // New parameters for runner timeout and verbose setup logging
            info += $"RunnerMaxBars: {RunnerMaxBars}\n";
            info += $"LogSetupsVerbose: {LogSetupsVerbose}\n";
            // Regime and equity logging info
            info += $"UseRegimes: {UseRegimes}\n";
            info += $"ActiveRegime: {ActiveRegime}\n";
            info += $"LogEquityCurve: {LogEquityCurve}\n";

            // Regime filter thresholds
            info += $"MinATRTrend: {MinATRTrend}\n";
            info += $"MinATRChop: {MinATRChop}\n";
            info += $"TrendSlopeMinTrend: {TrendSlopeMinTrend}\n";
            info += $"TrendSlopeMinChop: {TrendSlopeMinChop}\n";
            info += $"MinSpaceRTrend: {MinSpaceRTrend}\n";
            info += $"MinSpaceRChop: {MinSpaceRChop}\n";
            info += $"MinQTotal2Trend: {MinQTotal2Trend}\n";
            info += $"MinQTotal2Chop: {MinQTotal2Chop}\n";
            info += $"UseAutoRegimeSwitch: {UseAutoRegimeSwitch}\n";

            System.IO.File.WriteAllText(runInfoPath, info);

            logsReady = true;
        }

        private void LogTradeRow(
            string side,
            double entryPrice,
            double stopPrice,
            double targetPrice,
            int qty,
            string entryName)
        {
            EnsureLogsInitialized();
            string dateString = Time[0].ToString("yyyy-MM-dd");
            string timeString = Time[0].ToString("HH:mm:ss");

            double atrValue = (atrIndicator != null) ? atrIndicator[0] : double.NaN;
            double sessionWeight = 1.0;
            try { sessionWeight = Session_WeightNow(); } catch { }

            double pct = Helpers.Clamp01(lastRunnerPct);
            double runnerR = Math.Max(1.5, Math.Min(6.0, 1.0 / Math.Max(0.1, Math.Min(0.9, pct))));

            // Determine regime for current trade (if regimes enabled)
            string regime = ActiveRegime;
            string row = string.Format(
                "{0},{1},{2},{3:F2},{4:F2},{5:F2},{6},{7},{8},{9:F4},{10:F4},{11:F4},{12:F4},{13:F4},{14:F4},{15:F4},{16:F4},{17:F2},{18:F2},{19}",
                dateString,
                timeString,
                side,
                entryPrice,
                stopPrice,
                targetPrice,
                qty,
                BaseContracts,
                entryName,
                lastQTotal2,
                lastQSwing,
                lastQMomoRaw,
                lastQVol,
                lastQSession,
                Helpers.Clamp01(lastQRes / 2.0),
                lastQRes,
                atrValue,
                sessionWeight,
                pct,
                runnerR,
                regime
            );

            System.IO.File.AppendAllText(tradeLogPath, row + Environment.NewLine);
        }

        private void LogSetupRow(
            string evt,
            string side,
            string why,
            double trig,
            double stp,
            double tgt,
            double r,
            double stopTicks,
            string notes = "")
        {
            EnsureLogsInitialized();
            string dateString = Time[0].ToString("yyyy-MM-dd");
            string timeString = Time[0].ToString("HH:mm:ss");

            // include the current regime in the setup log
            string regime = ActiveRegime;
            string row = string.Format(
                "{0},{1},{2},{3},{4},{5:F2},{6:F2},{7:F2},{8:F2},{9:F2},{10},{11}",
                dateString,
                timeString,
                evt,
                side,
                why,
                trig,
                stp,
                tgt,
                r,
                stopTicks,
                notes,
                regime
            );
            System.IO.File.AppendAllText(setupLogPath, row + Environment.NewLine);
        }

        private void LogExit(string side, string entryName, double exitPrice, string exitType)
        {
            EnsureLogsInitialized();
            string dateString = Time[0].ToString("yyyy-MM-dd");
            string timeString = Time[0].ToString("HH:mm:ss");

            string row = string.Format(
                "{0},{1},{2},{3},{4:F2},{5}",
                dateString,
                timeString,
                side,
                entryName,
                exitPrice,
                exitType
            );
            System.IO.File.AppendAllText(exitLogPath, row + Environment.NewLine);
        }

        /// <summary>
        /// Log a completed trade with entry and exit details, PnL metrics and quality metrics.
        /// </summary>
        private void LogCompletedTrade(
            string entryDate,
            string entryTime,
            string exitDate,
            string exitTime,
            string side,
            string entryName,
            int qty,
            double entryPrice,
            double exitPrice,
            string exitType,
            int holdBars,
            double pnlTicks,
            double pnlPrice,
            double rMultiple,
            double qTotal2,
            double qSwing,
            double qMomo,
            double qVol,
            double qSession,
            double atrValue,
            double spaceR,
            string regime)
        {
            EnsureLogsInitialized();
            string row = string.Format(
                "{0},{1},{2},{3},{4},{5},{6},{7:F2},{8:F2},{9},{10:F2},{11:F2},{12:F2},{13:F4},{14:F4},{15:F4},{16:F4},{17:F4},{18:F4},{19:F4},{20}",
                entryDate,
                entryTime,
                exitDate + " " + exitTime,
                side,
                entryName,
                qty,
                entryPrice,
                exitPrice,
                exitType,
                holdBars,
                pnlTicks,
                pnlPrice,
                rMultiple,
                qTotal2,
                qSwing,
                qMomo,
                qVol,
                qSession,
                atrValue,
                spaceR,
                regime
            );
            System.IO.File.AppendAllText(completedTradesPath, row + Environment.NewLine);

            // Update equity curve if enabled
            if (LogEquityCurve)
            {
                cumulativePnL += pnlPrice;
                string eqRow = string.Format(
                    "{0},{1},{2:F2}",
                    exitDate,
                    exitTime,
                    cumulativePnL
                );
                System.IO.File.AppendAllText(equityCurvePath, eqRow + Environment.NewLine);
            }
        }
    }
}
