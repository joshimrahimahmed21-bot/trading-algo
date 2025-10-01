using System;
using System.IO;
using System.Collections.Generic;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
                using (var tw = new StreamWriter(tradeLogPath, false))
                    using (var sw = new StreamWriter(setupLogPath, false))
using NinjaTrader.Cbi;                       // <-- Execution, OrderState, MarketPosition
                using (var tw = new System.IO.StreamWriter(hardCsvPath, false, System.Text.Encoding.UTF8))


namespace NinjaTrader.NinjaScript.Strategies
    public partial class MNQRSTest : Strategy
    {
        /// <summary>
        /// Ensure log files are created and header written (for Trades and optionally Setups):contentReference[oaicite:135]{index=135}:contentReference[oaicite:136]{index=136}
        /// </summary>
        private void EnsureLogsInitialized()
        {
            if (IsInStrategyAnalyzer && !ExportDuringOptimization)
                return;
            if (logsReady)
                return;
            string logDir = (runStamp != null)
                ? System.IO.Path.Combine(Core.Globals.UserDataDir, "strategies", Name, runStamp)
                : System.IO.Path.Combine(Core.Globals.UserDataDir, "strategies", Name);
            try { System.IO.Directory.CreateDirectory(logDir); } catch { }
            // Prepare trade log CSV and write header if missing
            runStamp = runStamp ?? DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string tradeName = "MNQRSTest_Trades_" + runStamp + ".csv";
            tradeLogPath = System.IO.Path.Combine(logDir, tradeName);
            if (!File.Exists(tradeLogPath))
            {
                {
                    tw.WriteLine("Date,Time,Side,Entry,Stop,Target,R,Qty,Exit,PLTicks,PLUSD,ExitType,qSwing,qMomo,qVol,qSession,qTotal2,qResRun,oppATR,runwayR,lastRunnerPct,lastSizeBias,TrailLabel,TP1R,riskTicks,riskUsd,tpR,realizedR_raw,realizedR_norm,tpTicksD,Dual_VP_params_weights,UseDualVP,Minutes,VP_ShortWindow,VP_ShortResolution,VP_LongResolution,lastQ_VP_Tailwind,lastQ_VP_Headwind,lastQ_VP_Cushion,lastQ_ResVP_dir,lastVP_Congestion,VP_RegimeLabel,UseAutoRunner,UseAutoToggleRunnerPreset,UseVPRunnerScaling,VP_RunnerK1,VP_RunnerK2,RunnerBasePct,RunnerPctComputed,VP_Tailwind,VP_Headwind,VP_ResDir,Q_PosVol_RB,Q_PosVol_SB,Q_PosVol_LTF,Q_PosVol_Proxy,Q_PosVol_Proxy_Conf,FavMomo,TrueMomo,Q_Momo_ROC,Q_Momo_TSI,Q_Momo_MACD,Q_Momo_ER,Q_Momo_Streak,Q_Momo_Core,MomoConf");
                    // #CONFIG line with parameters echo
                    string cfg = BuildParamEcho();
                    string[] tcols = new string[] { "#CONFIG", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "Params", "", "", "", "", "", "", "", "", cfg };
                    tw.WriteLine(string.Join(",", tcols));
                }
            }
            // Prepare setup log if enabled:contentReference[oaicite:137]{index=137}
            if (ExportSetup)
            {
                string setupName = "MNQRSTest_Setups_" + runStamp + ".csv";
                setupLogPath = System.IO.Path.Combine(logDir, setupName);
                if (!File.Exists(setupLogPath))
                {
                    {
                        sw.WriteLine("Date,Time,Bar,Event,Side,Why,Trigger,Stop,Target,R,StopTicks,ATR?,Notes,ResistMissingFlag,Q_ResistMacro,Q_ResistSwing,Q_ResistBlend");
                        string cfgLine = string.Format("#CONFIG,{0},,Params,,,,,,,{1},{2}",
                                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                            (UseAtrBuffer ? 1 : 0),
                                            BuildParamEcho());
                        sw.WriteLine(cfgLine);
                    }
                }
            }
            logsReady = true;
            Print("[MNQ PARAMS] " + BuildParamEcho());
        }

        /// <summary>
        /// Log a setup event (armed, skip, expired, etc.) to the setup CSV (if enabled):contentReference[oaicite:138]{index=138}:contentReference[oaicite:139]{index=139}
        /// </summary>
        private void LogSetupRow(string evt, string side, string why,
                                 double trig, double stp, double tgt, double r,
                                 double stopTicks, string notes = "")
        {
            if (!ExportSetup)
                return;
            if (!logsReady)
            {
                try { EnsureLogsInitialized(); } catch { }
                if (!logsReady) return;
            }
            string row = string.Format("{0},{1},{2},{3},{4},{5},{6:F2},{7:F2},{8:F2},{9:F2},{10:F1},{11},{12},{13},{14:F2},{15:F2},{16:F2}",
                                       Time[0].ToString("yyyy-MM-dd"),
                                       Time[0].ToString("HH:mm:ss"),
                                       CurrentBar,
                                       evt,
                                       side,
                                       (why ?? ""),
                                       trig, stp, tgt, r, stopTicks,
                                       "",  // ATR? placeholder
                                       (notes ?? "").Replace(",", ";"),
                                       lastResistMissingFlag,
                                       (double.IsNaN(lastQResistMacro) ? ResistMissingValue : lastQResistMacro),
                                       (double.IsNaN(lastQResistSwing) ? ResistMissingValue : lastQResistSwing),
                                       (double.IsNaN(lastQResistBlend) ? ResistMissingValue : lastQResistBlend));
            // Write to setup log (disabled by default to avoid performance hit during playback)
            // File.AppendAllText(setupLogPath, row + Environment.NewLine);
        }

        /// <summary>
        /// Build a TradeRow object from current context for export (via Exporter or logs):contentReference[oaicite:140]{index=140}:contentReference[oaicite:141]{index=141}
        /// </summary>
        private TradeRow BuildTradeRowFromContext(string exitType, double execPrice = 0.0)
        {
            double entryPrice = 0.0;
            try { entryPrice = triggerPrice; } catch (Exception ex) { Print("[BuildTradeRow:entry] " + ex.Message); }
            double stopPrice = 0.0;
            try { stopPrice = plannedStopPrice; } catch (Exception ex) { Print("[BuildTradeRow:stop] " + ex.Message); }
            double targetPrice = 0.0;
            try { targetPrice = plannedTargetPrice; } catch (Exception ex) { Print("[BuildTradeRow:target] " + ex.Message); }
            int qty = 0;
            try { qty = plannedQty; } catch (Exception ex) { Print("[BuildTradeRow:qty] " + ex.Message); }
            int riskTicks = 0;
            try { riskTicks = (int)Math.Round(Math.Abs(plannedStopPrice - triggerPrice) / Instrument.MasterInstrument.TickSize); } catch { }
            double plUsd = 0.0;
            try { plUsd = lastPLUsd; } catch (Exception ex) { Print("[BuildTradeRow:plUsd] " + ex.Message); }
            int plTicks = 0;
            try { plTicks = lastPLTicks; } catch (Exception ex) { Print("[BuildTradeRow:plTicks] " + ex.Message); }
            string side = "Flat";
            try
            {
                side = Position.MarketPosition == MarketPosition.Long ? "Long"
                     : Position.MarketPosition == MarketPosition.Short ? "Short" : "Flat";
            }
            catch { }
            double exitPx = (execPrice != 0.0 ? execPrice : (Close != null && Close.Count > 0 ? Close[0] : 0.0));
            // Quality telemetry values with safe fallback:contentReference[oaicite:142]{index=142}:contentReference[oaicite:143]{index=143}
            double qSwing = 0, qMomo = 0, qVol = 0, qSess = 0, qTot2 = 0, qResRun = 0;
            try { qSwing = lastQSwing; } catch { }
            try { qMomo  = lastQMomo; } catch { }
            try { qVol   = lastQVol; } catch { }
            try { qSess  = lastQSession; } catch { }
            try { qTot2  = lastQTotal2; } catch { }
            try { qResRun= lastQRes; } catch { }  // runner runway quality
            // Runner and sizing metrics:contentReference[oaicite:144]{index=144}:contentReference[oaicite:145]{index=145}
            double runnerPct = 0, sizeBias = 0, riskUsd = 0, tpR = 0, realRaw = 0, realNorm = 0;
            int tpTicksD = 0;
            try { runnerPct = lastRunnerPct; } catch { }
            try { sizeBias  = lastSizeBias; } catch { }
            try { riskUsd   = riskTicks * (Instrument.MasterInstrument.PointValue * Instrument.MasterInstrument.TickSize); } catch { }
            try { tpR       = 0; } catch { }  // not used
            try { realRaw   = 0; } catch { }
            try { realNorm  = 0; } catch { }
            try { tpTicksD  = 0; } catch { }
            // Volume profile metrics:contentReference[oaicite:146]{index=146}:contentReference[oaicite:147]{index=147}
            double vpTail = 0, vpHead = 0, vpCush = 0, vpResDir = 0, vpCong = 0;
            string vpRegime = "";
            try { vpTail = lastQ_VP_Tailwind; } catch { }
            try { vpHead = lastQ_VP_Headwind; } catch { }
            try { vpCush = lastQ_VP_Cushion; } catch { }
            try { vpResDir = lastQ_ResVP_dir; } catch { }
            try { vpCong = lastVP_Congestion; } catch { }
            try { vpRegime = lastVP_RegimeLabel; } catch { }
            // Runner base percentage if adjusted
            double runnerBase = runnerPct;
            try { runnerBase = lastRunnerBasePct; } catch { }
            return new TradeRow
            {
                Time = Time[0],
                Side = side,
                Entry = entryPrice,
                Stop = stopPrice,
                Target = targetPrice,
                R = riskTicks,
                Qty = qty,
                Exit = exitPx,
                PLTicks = plTicks,
                PLUSD = plUsd,
                ExitType = exitType ?? "",
                Q_Swing = qSwing,
                Q_Momo = qMomo,
                Q_Vol = qVol,
                QSession = qSess,
                QTotal2 = qTot2,
                QResRun = qResRun,
                oppATR = 0.0,            // populated via HardLock logging
                runwayR = 0.0,          // populated via HardLock logging
                lastRunnerPct = runnerPct,
                lastSizeBias = sizeBias,
                TrailLabel = lastTrailLabel,
                TP1R = (lastTPPlan.Contains("Extend") ? "TP1R_Extend" : "TP1R"),
                riskTicks = riskTicks,
                riskUsd = riskUsd,
                tpR = tpR,
                realizedR_raw = realRaw,
                realizedR_norm = realNorm,
                tpTicksD = tpTicksD,
                DualVP_params_weights = "",  // placeholder (not needed in object)
                UseDualVP = UseDualVP,
                Minutes = VP_Window,
                VP_ShortWindow = VP_ShortWindow,
                VP_ShortResolution = VP_ShortResolution,
                VP_LongResolution = VP_LongResolution,
                lastQ_VP_Tailwind = vpTail,
                lastQ_VP_Headwind = vpHead,
                lastQ_VP_Cushion = vpCush,
                lastQ_ResVP_dir = vpResDir,
                lastVP_Congestion = vpCong,
                VP_RegimeLabel = vpRegime,
                UseAutoRunner = false,  // not explicitly used (legacy)
                UseAutoToggleRunnerPreset = AutoToggleRunnerPreset,
                UseVPRunnerScaling = UseVPRunnerScaling,
                VP_RunnerK1 = VP_RunnerK1,
                VP_RunnerK2 = VP_RunnerK2,
                RunnerBasePct = runnerBase,
                RunnerPctComputed = runnerPct,
                VP_Tailwind = vpTail,
                VP_Headwind = vpHead,
                VP_ResDir = vpResDir
            };
        }

        /// <summary>
        /// Append a debug event for strategy state (Primarily used for debugging pipeline):contentReference[oaicite:148]{index=148}:contentReference[oaicite:149]{index=149}
        /// </summary>
        private void WriteDebugSetup(string tag, string notes)
        {
            if (!logsReady)
            {
                try { EnsureLogsInitialized(); } catch { }
                if (!logsReady) return;
            }
            string line = string.Format("#DEBUG,{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10:F2},{11:F2},{12:F2},{13:F2},{14}",
                             Time[0].ToString("yyyy-MM-dd"),
                             Time[0].ToString("HH:mm:ss"),
                             CurrentBar,
                             ToTime(Time[0]),
                             UseVolumeProfile,
                             UseSwingHTF,
                             QualitySafeMode,
                             armedLong,
                             armedShort,
                             lastResistMissingFlag,
                             lastQSwing,
                             lastQMomoRaw,
                             lastQVol,
                             lastQTotal2,
                             tag + "|" + (notes ?? "").Replace(",", ";"));
            // Setup log append disabled by default for performance (telemetry only)
            // File.AppendAllText(setupLogPath, line + Environment.NewLine);
        }

        /// <summary>
        /// Build a semicolon-separated string echoing key parameter settings (for logging).
        /// </summary>
        private string BuildParamEcho()
        {
            List<string> parts = new List<string>();
            // We include a subset of important settings for brevity
            parts.Add("RiskPerTradeUSD=" + RiskPerTradeUSD);
            parts.Add("MinSpaceR=" + MinSpaceR);
            parts.Add("UseVolumeProfile=" + UseVolumeProfile);
            parts.Add("SizeFactorLow=" + SizeFactorLow.ToString("F2"));
            parts.Add("UseAnchor2=" + UseAnchor2);
            parts.Add("Anchor2Hour=" + Anchor2Hour);
            parts.Add("Anchor2Minute=" + Anchor2Minute);
            parts.Add("UseAnchor3=" + UseAnchor3);
            parts.Add("Anchor3Hour=" + Anchor3Hour);
            parts.Add("Anchor3Minute=" + Anchor3Minute);
            return string.Join(";", parts);
        }
    }
}


        // === PosVol telemetry appender ===
        private void AppendPosVolCsv(System.IO.TextWriter tw)
        {
            try
            {
                tw.Write("," + lastQ_PosVol_RB.ToString(System.Globalization.CultureInfo.InvariantCulture));
                tw.Write("," + lastQ_PosVol_SB.ToString(System.Globalization.CultureInfo.InvariantCulture));
                tw.Write("," + lastQ_PosVol_LTF.ToString(System.Globalization.CultureInfo.InvariantCulture));
                tw.Write("," + lastQ_PosVol_Proxy.ToString(System.Globalization.CultureInfo.InvariantCulture));
                tw.Write("," + lastQ_PosVol_Proxy_Conf.ToString(System.Globalization.CultureInfo.InvariantCulture));
                tw.Write("," + lastFavMomo.ToString(System.Globalization.CultureInfo.InvariantCulture));
                tw.Write("," + lastTrueMomo.ToString(System.Globalization.CultureInfo.InvariantCulture));
            // MomentumCore telemetry (observe-only; values clamped in their producers)
            try
            {
                tw.Write("," + lastQ_Momo_ROC.ToString(System.Globalization.CultureInfo.InvariantCulture));
                tw.Write("," + lastQ_Momo_TSI.ToString(System.Globalization.CultureInfo.InvariantCulture));
                tw.Write("," + lastQ_Momo_MACD.ToString(System.Globalization.CultureInfo.InvariantCulture));
                tw.Write("," + lastQ_Momo_ER.ToString(System.Globalization.CultureInfo.InvariantCulture));
                tw.Write("," + lastQ_Momo_Streak.ToString(System.Globalization.CultureInfo.InvariantCulture));
                tw.Write("," + lastQ_Momo_Core.ToString(System.Globalization.CultureInfo.InvariantCulture));
                tw.Write("," + lastMomoConf.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch { /* keep logging robust */ }

            } catch { }
        }
    public partial class MNQRSTest : Strategy
    {
        // Helper functions for clamping/blending
        private static double Clamp01(double x) => (x < 0.0 ? 0.0 : (x > 1.0 ? 1.0 : x));
        private static double Squash(double x) => 0.5 * (Math.Tanh(x) + 1.0);
        private static double Blend(double a, double b, double w) => (1 - w) * a + w * b;
    }
}
// Utility classes for rolling statistics and EMA
{
    // RollingStats: maintains a window of values for z-score calculation
    public class RollingStats
    {
        private readonly int maxLength;
        private readonly Queue<double> window;
        public RollingStats(int length) { maxLength = length; window = new Queue<double>(); }
        public double UpdateAndZ(double value)
        {
            window.Enqueue(value);
            if (window.Count > maxLength)
                window.Dequeue();
            int n = window.Count;
            if (n == 0) return 0.0;
            double sum = 0.0, sumSq = 0.0;
            foreach (double v in window) { sum += v; sumSq += v * v; }
            double mean = sum / n;
            double var = (sumSq / n) - (mean * mean);
            if (var < 1e-12) return 0.0;
            double stdDev = Math.Sqrt(var);
            return (value - mean) / stdDev;
        }
    }
    
    // Ema: simple exponential moving average for double values
    public class Ema
    {
        private readonly double alpha;
        private bool hasValue;
        private double ema;
        public Ema(int period)
        {
            if (period < 1) period = 1;
            alpha = 2.0 / (period + 1);
            hasValue = false;
            ema = 0.0;
        }
        public double Update(double value)
        {
            if (!hasValue)
            {
                ema = value;
                hasValue = true;
            }
            else
            {
                ema = alpha * value + (1 - alpha) * ema;
            }
            return ema;
        }
    }
}

    public partial class MNQRSTest : Strategy
    {
        // HardLock state fields for execution tracking
        private MarketPosition hard_posCache = MarketPosition.Flat;
        private string hard_lastNonFlatSide = "";
        private string hard_sideCache = "?";
        private int hard_qtyCache = 0;
        private double hard_entryCache = double.NaN;
        private double hard_exitCache = double.NaN;
        private string hard_exitTypeCache = "";
        private double hard_lastRunnerPctCache = 0.0;
        private double hard_lastRunnerRiskTicksCache = 0.0;
        // Log telemetry cache (to preserve at fill events):contentReference[oaicite:150]{index=150}
        private double qSwing_log = double.NaN, qMomo_log = double.NaN, qVol_log = double.NaN, qSession_log = double.NaN;
        private double qTotal2_log = double.NaN, qResRun_log = double.NaN, oppATR_log = double.NaN, runwayR_log = double.NaN, nextOppRatio_log = double.NaN;
        private string hard_lastTrailLabel = "";
        private double hard_lastRCache = 0.0;
        private double hard_lastTargetCache = double.NaN;
        private double hard_lastStopCache = double.NaN;
        private double hard_lastEntryCache = double.NaN;
        // HardLock CSV output control
        private string hardCsvPath;
        private bool hardHeaderWrote = false;
        private int hardHeaderCols = 52;
        private DateTime hard_lastWriteTime = DateTime.MinValue;
        private double hard_lastWritePrice = 0.0;
        private int hard_lastWriteQty = 0;

        /// <summary>
        /// Initialize HardLock CSV file (only once per strategy run):contentReference[oaicite:151]{index=151}:contentReference[oaicite:152]{index=152}
        /// </summary>
        private void HardLock_Init()
        {
            if (hardHeaderWrote) return;
            try
            {
                string folder = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                    "NinjaTrader 8", "bin", "Custom", "Logs", "MNQRSTest");
                System.IO.Directory.CreateDirectory(folder);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                hardCsvPath = System.IO.Path.Combine(folder, string.Format("Trades_{0}_{1}_{2}.csv", stamp, BatchTag, "HARD_vDBG1"));
                {
                    tw.WriteLine("Date,Time,Side,Entry,Stop,Target,R,Qty,Exit,PLTicks,PLUSD,ExitType,qSwing,qMomo,qVol,qSession,qTotal2,qResRun,oppATR,runwayR,lastRunnerPct,lastSizeBias,TrailLabel,TP1R,riskTicks,riskUsd,tpR,realizedR_raw,realizedR_norm,tpTicksD,Dual_VP_params_weights,UseDualVP,Minutes,VP_ShortWindow,VP_ShortResolution,VP_LongResolution,lastQ_VP_Tailwind,lastQ_VP_Headwind,lastQ_VP_Cushion,lastQ_ResVP_dir,lastVP_Congestion,VP_RegimeLabel,UseAutoRunner,UseAutoToggleRunnerPreset,UseVPRunnerScaling,VP_RunnerK1,VP_RunnerK2,RunnerBasePct,RunnerPctComputed,VP_Tailwind,VP_Headwind,VP_ResDir");
                    hardHeaderCols = 52;
                }
                hardHeaderWrote = true;
                Print($"[MNQ HL] {hardCsvPath}");
            }
            catch (Exception ex)
            {
                Print($"[MNQ HL Init EX] {ex.Message}");
            }
        }

        /// <summary>
        /// OnExecutionUpdate: (not used for final logging in HardLock mode; reserved for possible partial fill logging)
        /// </summary>
        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            // (No action here for HardLock final logging - OnOrderUpdate handles at fill completion)
        }

        /// <summary>
        /// OnOrderUpdate: track fills and perform HardLock CSV logging when trade completes:contentReference[oaicite:153]{index=153}:contentReference[oaicite:154]{index=154}
        /// </summary>
        protected override void OnOrderUpdate(Cbi.Order order, double limitPrice, double stopPrice, int quantity,
                                              int filled, double averageFillPrice, Cbi.OrderState orderState,
                                              DateTime time, Cbi.ErrorCode error, string comment)
        {
            if (order == null || order.Name == null) return;
            // If an entry order gets cancelled or rejected, clear armed flags:contentReference[oaicite:155]{index=155}
            if ((orderState == OrderState.Cancelled || orderState == OrderState.Rejected) &&
                (order.Name == "LongEntry" || order.Name == "ShortEntry"))
            {
                Print($"[ORDER {orderState}] {order.Name}  Reason={comment}");
                armedLong = armedShort = false;
            }
            // On each filled event, update openQty and caches:contentReference[oaicite:156]{index=156}:contentReference[oaicite:157]{index=157}
            if (orderState == OrderState.Filled)
            {
                // Debounce duplicate events for same fill:contentReference[oaicite:158]{index=158}
                double tick = Instrument != null ? Instrument.MasterInstrument.TickSize : 1.0;
                bool isDup = (hard_lastWriteTime == time) && (Math.Abs(hard_lastWritePrice - averageFillPrice) < tick / 2.0) && (hard_lastWriteQty == quantity);
                if (!isDup)
                {
                    // Determine fill side (L/S) from order name or current position:contentReference[oaicite:159]{index=159}
                    string _side = (order.Name.Contains("Long") ? "L" : order.Name.Contains("Short") ? "S"
                                        : (Position.MarketPosition == MarketPosition.Short ? "S"
                                           : Position.MarketPosition == MarketPosition.Long ? "L" : "F"));
                    // Track cumulative open position quantity (positive for long, negative for short):contentReference[oaicite:160]{index=160}:contentReference[oaicite:161]{index=161}
                    bool wasFlat = (hard_qtyCache == 0);
                    int signedQty = quantity * (_side == "S" ? -1 : 1);
                    hard_qtyCache += signedQty;
                    if (wasFlat && hard_qtyCache != 0)
                    {
                        // Position just opened
                        hard_lastEntryPrice = averageFillPrice;
                        hard_lastNonFlatSide = (_side == "L" ? "Long" : "Short");
                        hard_sideCache = (_side == "L" ? "L" : "S");
                        // Store initial trade context in caches for final logging
                        hard_lastTrailLabel = lastTrailLabel;
                        hard_lastEntryCache = averageFillPrice;
                        hard_lastStopCache = plannedStopPrice;
                        hard_lastTargetCache = plannedTargetPrice;
                        hard_lastRCache = RewardMultiple;
                        hard_lastRunnerPctCache = lastRunnerPct;
                        hard_lastRunnerRiskTicksCache = lastRunnerRiskTicks;
                    }
                    // If this fill closes the position (openQty becomes 0):contentReference[oaicite:162]{index=162}:contentReference[oaicite:163]{index=163}
                    if (hard_qtyCache == 0)
                    {
                        // Determine exit type (TP/SL) based on order name and P/L sign
                        string exitType = "";
                        if (order.Name.Contains("Stop")) exitType = "SL";
                        else if (order.Name.Contains("Profit") || order.Name.Contains("TP")) exitType = "TP";
                        // Save last exit fill price/time for logging
                        hard_exitCache = averageFillPrice;
                        hard_exitTypeCache = exitType;
                        hard_lastNonFlatSide = (_side == "L" ? "Long" : "Short");  // side of the trade that just closed
                        // Perform final logging to CSV
                        HardLock_Init();
                        if (hardHeaderWrote)
                        {
                            try
                            {
                                HardLock_BuildAndWriteFullRow();
                            }
                            catch (Exception ex) { Print("[AutoCatch] " + ex.Message); }
                        }
                        // Reset caches after logging
                        hard_lastNonFlatSide = (_side == "L" ? "Long" : "Short");
                    }
                    // Update last write markers for duplicate detection
                    hard_lastWriteTime = time;
                    hard_lastWritePrice = averageFillPrice;
                    hard_lastWriteQty = quantity;
                }
            }
        }

        /// <summary>
        /// Build and append the full trade record CSV line to the HardLock file (called when trade closes):contentReference[oaicite:164]{index=164}:contentReference[oaicite:165]{index=165}
        /// </summary>
        private void HardLock_BuildAndWriteFullRow()
        {
            if (CurrentBar < BarsRequiredToTrade) return;
            List<string> cols = new List<string>();
            // Initialize local variables for CSV fields
            string side = "?"; 
            int qty = 0;
            string exitType = "";
            if (side == "?" && !string.IsNullOrEmpty(hard_lastNonFlatSide))
                side = hard_lastNonFlatSide;
            // Set defaults using current Close for any missing values
            double entry = Close[0], stp = Close[0], tgt = Close[0], exit = Close[0];
            double dir = 0.0, risk = 0.0, r = 0.0;
            // Fill with cached values if available:contentReference[oaicite:166]{index=166}:contentReference[oaicite:167]{index=167}
            if (!double.IsNaN(hard_lastEntryCache)) entry = hard_lastEntryCache;
            if (!double.IsNaN(hard_lastStopCache)) stp = hard_lastStopCache;
            if (!double.IsNaN(hard_lastTargetCache)) tgt = hard_lastTargetCache;
            if (r == 0.0) r = hard_lastRCache;
            if (string.IsNullOrEmpty(exitType) || exitType == "Filled")
            {
                if (side == "?" && !string.IsNullOrEmpty(hard_lastNonFlatSide))
                    side = hard_lastNonFlatSide;
                if (qty == 0 && hard_lastWriteQty > 0)
                    qty = hard_lastWriteQty;
            }
            if (lastRunnerPct <= 0.0)
            {
                try
                {
                    lastRunnerBasePct = hard_lastRunnerPctCache;
                    lastRunnerPct = hard_lastRunnerPctCache;
                }
                catch { }
            }
            if (string.IsNullOrEmpty(lastTanhBandLabel))
            {
                try { lastTanhBandLabel = hard_lastTrailLabel; } catch { }
            }
            // Determine final trade side and quantity
            if (Position.MarketPosition != MarketPosition.Flat)
            {
                side = (Position.MarketPosition == MarketPosition.Long ? "L" : "S");
                qty = Math.Abs(Position.Quantity);
            }
            else
            {
                side = (hard_sideCache == "L" ? "L" : hard_sideCache == "S" ? "S" : "F");
                qty = hard_qtyCache;
            }
            // Determine final entry/exit prices and stop/target based on position or cache
            if (Position.MarketPosition != MarketPosition.Flat)
            {
                entry = Position.AveragePrice;
            }
            else
            {
                if (!double.IsNaN(hard_entryCache))
                    entry = hard_entryCache;
            }
            if (Position.MarketPosition == MarketPosition.Flat && !double.IsNaN(hard_exitCache))
                exit = hard_exitCache;
            if (Position.MarketPosition == MarketPosition.Long)
            {
                stp = Math.Min(Close[0], Low[0]);
                tgt = Math.Max(Close[0], High[0]);
            }
            else if (Position.MarketPosition == MarketPosition.Short)
            {
                stp = Math.Max(Close[0], High[0]);
                tgt = Math.Min(Close[0], Low[0]);
            }
            else
            {
                if (!double.IsNaN(hard_entryCache))
                {
                    stp = hard_entryCache;
                    tgt = hard_entryCache;
                }
            }
            dir = (side == "L" ? 1.0 : side == "S" ? -1.0 : 0.0);
            risk = Math.Abs(entry - stp);
            if (dir != 0.0 && risk > 0.0)
                r = dir * (tgt - entry) / risk;
            // Compute P/L in ticks and USD
            double plTicks = (dir != 0.0 ? (exit - entry) / Instrument.MasterInstrument.TickSize * dir : 0.0);
            double tickValUSD = Instrument.MasterInstrument.PointValue * Instrument.MasterInstrument.TickSize;
            double plUsd = plTicks * tickValUSD;
            // Determine exit type from result if not set by order:contentReference[oaicite:171]{index=171}
            if (string.IsNullOrEmpty(exitType))
            {
                exitType = (plTicks >= 0 ? "TP" : "SL");
                if (Math.Abs(plTicks) < 0.5) 
                    exitType = "BE";
            }
            // Fill CSV columns in sequence
            cols.Add(Time[0].ToString("yyyy-MM-dd"));
            cols.Add(Time[0].ToString("HH:mm:ss"));
            cols.Add(side == "L" ? "L" : side == "S" ? "S" : "F");
            cols.Add(entry.ToString("F2"));
            cols.Add(stp.ToString("F2"));
            cols.Add(tgt.ToString("F2"));
            cols.Add(r.ToString("F2"));
            cols.Add(qty.ToString());
            cols.Add(exit.ToString("F2"));
            cols.Add(((int)Math.Round(plTicks)).ToString());
            cols.Add(plUsd.ToString("F2"));
            cols.Add(exitType);
            cols.Add(lastQSwing.ToString("F2"));
            cols.Add(lastQMomoRaw.ToString("F2"));
            cols.Add(lastQVol.ToString("F2"));
            cols.Add(lastQSession.ToString("F2"));
            cols.Add(lastQTotal2.ToString("F2"));
            cols.Add(lastQRes.ToString("F2"));
            // For oppATR and runwayR (runner runway in ATR units and R units), we use cached metrics:
            double oppATR = lastSpace_NextOpposite_ATR;
            double runwayR = 0.0;
            if (lastSpace_ATR_Ticks > 0 && lastSpace_RiskTicks > 0)
            {
                double riskR_local = lastSpace_RiskTicks / lastSpace_ATR_Ticks;
                double adjATR_local = Math.Max(0.0, lastSpace_AdjTicks) / lastSpace_ATR_Ticks;
                if (riskR_local > 1e-9)
                    runwayR = Math.Min(Space_MaxRCap, adjATR_local / riskR_local);
            }
            cols.Add(oppATR.ToString("F2"));
            cols.Add(runwayR.ToString("F2"));
            cols.Add(lastRunnerPct.ToString("F2"));
            cols.Add(lastSizeBias.ToString("F2"));
            cols.Add(lastTrailLabel ?? "");
            cols.Add(lastTPPlan);
            cols.Add(((int)Math.Round(Math.Abs(stp - entry) / Instrument.MasterInstrument.TickSize)).ToString());
            cols.Add((Math.Abs(stp - entry) * tickValUSD).ToString("F2"));
            cols.Add("0.00");
            cols.Add("0.00");
            cols.Add("0.00");
            cols.Add("0.00");
            cols.Add("0");
            cols.Add("");  // Dual_VP_params_weights placeholder
            cols.Add(UseDualVP ? "1" : "0");
            cols.Add(VP_Window.ToString());
            cols.Add(VP_ShortWindow.ToString());
            cols.Add(VP_ShortResolution.ToString());
            cols.Add(VP_LongResolution.ToString());
            cols.Add(lastQ_VP_Tailwind.ToString("F2"));
            cols.Add(lastQ_VP_Headwind.ToString("F2"));
            cols.Add(lastQ_VP_Cushion.ToString("F2"));
            cols.Add(lastQ_ResVP_dir.ToString("F2"));
            cols.Add(lastVP_Congestion.ToString("F2"));
            cols.Add(lastVP_RegimeLabel ?? "Basic");
            cols.Add("0");  // UseAutoRunner (not used, default 0)
            cols.Add(AutoToggleRunnerPreset ? "1" : "0");
            cols.Add(UseVPRunnerScaling ? "1" : "0");
            cols.Add(VP_RunnerK1.ToString("F2"));
            cols.Add(VP_RunnerK2.ToString("F2"));
            cols.Add(lastRunnerBasePct.ToString("F2"));
            cols.Add(lastRunnerPct.ToString("F2"));
            cols.Add(lastQ_VP_Tailwind.ToString("F2"));
            cols.Add(lastQ_VP_Headwind.ToString("F2"));
            cols.Add(lastQ_ResVP_dir.ToString("F2"));
            // Pad or trim columns to expected count:contentReference[oaicite:172]{index=172}
            while (cols.Count < hardHeaderCols)
                cols.Add(string.Empty);
            if (cols.Count > hardHeaderCols)
                cols = cols.GetRange(0, hardHeaderCols);
            // Append the CSV line to file
            string line = string.Join(",", cols);
            File.AppendAllText(hardCsvPath, line + System.Environment.NewLine, System.Text.Encoding.UTF8);
        }
    }
}


    public partial class MNQRSTest : Strategy
    {
        // --- Fields expected by HardLock (single canonical definitions) ---
        private double hard_lastEntryPrice = double.NaN;

        // Space snapshots used by HardLock rows (ticks)
        private int lastSpace_ATR_Ticks  = 0;  // ATR-sized buffer in ticks at time of entry/snapshot
        private int lastSpace_RiskTicks  = 0;  // risk ticks between trigger and stop
        private int lastSpace_AdjTicks   = 0;  // adjusted runway ticks (e.g., after ATR buffer)

        /// <summary>
        /// Refresh the space snapshot so HardLock can log consistent values.
        /// Call this where you arm/trigger a trade (or just before logging).
        /// </summary>
        private void UpdateSpaceSnapshot()
        {
            double tick = Math.Max(Instrument.MasterInstrument.TickSize, 1e-9);

            // Risk ticks from current planned trigger/stop (fallback to bar range)
            int riskTicks = 1;
            if (!double.IsNaN(plannedStopPrice) && !double.IsNaN(triggerPrice) && plannedStopPrice > 0 && triggerPrice > 0)
                riskTicks = Math.Max(1, (int)Math.Round(Math.Abs(plannedStopPrice - triggerPrice) / tick));
            else
                riskTicks = Math.Max(1, (int)Math.Round(Math.Abs(High[0] - Low[0]) / tick));

            lastSpace_RiskTicks = riskTicks;

            // ATR ticks (fallback to risk if ATR not available)
            try
            {
                double atrTicks = ATR(Math.Max(10, QPM_ATRLen))[0] / tick;
                lastSpace_ATR_Ticks = Math.Max(1, (int)Math.Round(atrTicks));
            }
            catch
            {
                lastSpace_ATR_Ticks = riskTicks;

            // Adjusted runway ticks: if you have a real space calc, use it; else mirror risk
            lastSpace_AdjTicks = lastSpace_RiskTicks;
