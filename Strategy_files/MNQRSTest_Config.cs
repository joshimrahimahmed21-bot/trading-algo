using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Core parameter and shared-field definition for the MNQRSTest strategy.  This partial consolidates
    /// all public NinjaScript properties and default values into a single location and introduces
    /// private fields used across other partials.  It also implements OnStateChange to set defaults
    /// and perform one-time initialization.
    /// </summary>
    public partial class MNQRSTest : Strategy
    {
        // --- Shared field declarations ---
        // Indicator helpers used by quality metrics
        private RollingStats deltaStats;
        private Ema dirEma;
        private Ema deltaEma;
        private RSI rsiIndicator;
        private ADX adxIndicator;

        // Price EMA for entry signal detection
        private EMA priceEma;

        // ATR indicator for volatility filter
        private ATR atrIndicator;

        // Runner and sizing state
        private double lastRunnerPct;
        private double lastRunnerBasePct;
        private double lastSizeBias;
        private double plannedStopPrice;
        private double plannedTargetPrice;
        private double triggerPrice;

        // Volume profile/VP state
        private string lastTrailLabel;
        private string lastTrailType;
        private double lastQ_VP_Tailwind;
        private double lastQ_VP_Headwind;
        private double lastQ_VP_Cushion;
        private double lastQ_ResVP_dir;
        private double lastVP_Congestion;
        private string lastVP_RegimeLabel;
        private bool UseVPTrailSwitch;
        private bool UseVPRunnerScaling;
        private double VP_RunnerK1;
        private double VP_RunnerK2;
        private int VPCongestionLookbackMins;
        private int VP_ShortWindow;
        private int VP_ShortResolution;
        private int VP_LongResolution;
        private int HysteresisBars;

        // Entry filter toggles and parameters
        [NinjaScriptProperty]
        [Display(Name = "UseEntryTimeFilter", GroupName = "Entry Filters", Order = 60)]
        public bool UseEntryTimeFilter { get; set; }

        [NinjaScriptProperty][Range(0, 23)]
        [Display(Name = "EntryStartHour", GroupName = "Entry Filters", Order = 61)]
        public int EntryStartHour { get; set; }
        [NinjaScriptProperty][Range(0, 59)]
        [Display(Name = "EntryStartMinute", GroupName = "Entry Filters", Order = 62)]
        public int EntryStartMinute { get; set; }
        [NinjaScriptProperty][Range(0, 23)]
        [Display(Name = "EntryEndHour", GroupName = "Entry Filters", Order = 63)]
        public int EntryEndHour { get; set; }
        [NinjaScriptProperty][Range(0, 59)]
        [Display(Name = "EntryEndMinute", GroupName = "Entry Filters", Order = 64)]
        public int EntryEndMinute { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "UseVolatilityFilter", GroupName = "Entry Filters", Order = 65)]
        public bool UseVolatilityFilter { get; set; }
        [NinjaScriptProperty][Range(0.0, 1000.0)]
		[Display(Name="MinATR", GroupName="Entry Filters", Order=66)]
		public double MinATR { get; set; }
        [NinjaScriptProperty][Range(0.0, 1000000.0)]
        [Display(Name = "MaxATR", GroupName = "Entry Filters", Order = 67)]
        public double MaxATR { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "UseTrendFilter", GroupName = "Entry Filters", Order = 68)]
        public bool UseTrendFilter { get; set; }
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "TrendSlopeMin", GroupName = "Entry Filters", Order = 69)]
        public double TrendSlopeMin { get; set; }

        // Positional volume proxy values (exposed via properties elsewhere)
        private double lastQ_PosVol_Proxy = 0.5;
        private double lastQ_PosVol_Proxy_Conf = 0.8;

        // Resist and logging state placeholders
        private double lastResistMissingFlag;
        private double lastQResistMacro;
        private double lastQResistSwing;
        private double lastQResistBlend;
        private bool logsReady;
        private bool ExportSetup;
        private bool ExportDuringOptimization;
        private bool UseAtrBuffer;
        private string runStamp;

        // Q metric placeholders
        private double lastQSwing, lastQMomoRaw, lastQVol, lastQSession, lastQTotal2, lastQRes;
        private double lastQMomoCore;

        // --- NinjaScript properties (public parameters) ---
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "W_PosVolProxy", GroupName = "Parameters", Order = 1)]
        public double W_PosVolProxy { get; set; }

        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "W_FavMomo", GroupName = "Parameters", Order = 2)]
        public double W_FavMomo { get; set; }

        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "W_TrueMomo", GroupName = "Parameters", Order = 3)]
        public double W_TrueMomo { get; set; }
		
		[NinjaScriptProperty][Range(0.0, 1.0)]
[Display(Name = "W_QSwing", GroupName = "Quality Weights", Order = 60)]
public double W_QSwing { get; set; }

[NinjaScriptProperty][Range(0.0, 1.0)]
[Display(Name = "W_QMomo", GroupName = "Quality Weights", Order = 61)]
public double W_QMomo { get; set; }

[NinjaScriptProperty][Range(0.0, 1.0)]
[Display(Name = "W_QVol", GroupName = "Quality Weights", Order = 62)]
public double W_QVol { get; set; }

[NinjaScriptProperty][Range(0.0, 1.0)]
[Display(Name = "W_QSession", GroupName = "Quality Weights", Order = 63)]
public double W_QSession { get; set; }

[NinjaScriptProperty][Range(0.0, 1.0)]
[Display(Name = "RunnerMomoThreshold", GroupName = "Runner", Order = 300)]
public double RunnerMomoThreshold { get; set; }

[NinjaScriptProperty][Range(0.0, 10.0)]
[Display(Name = "RunnerSpaceThreshold", GroupName = "Runner", Order = 301)]
public double RunnerSpaceThreshold { get; set; }


        // Momentum core weights
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "W_MomoROC", GroupName = "MomentumCore Weights", Order = 10)]
        public double W_MomoROC { get; set; }
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "W_MomoTSI", GroupName = "MomentumCore Weights", Order = 11)]
        public double W_MomoTSI { get; set; }
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "W_MomoMACD", GroupName = "MomentumCore Weights", Order = 12)]
        public double W_MomoMACD { get; set; }
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "W_MomoER", GroupName = "MomentumCore Weights", Order = 13)]
        public double W_MomoER { get; set; }
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "W_MomoStreak", GroupName = "MomentumCore Weights", Order = 14)]
        public double W_MomoStreak { get; set; }

        // Additional new weights
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "W_PosVolVP", GroupName = "Parameters", Order = 4)]
        public double W_PosVolVP { get; set; }
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "W_PosRes", GroupName = "Parameters", Order = 5)]
        public double W_PosRes { get; set; }

        // Favoured momentum amplification
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "FavMomoAmplifier", GroupName = "Parameters", Order = 6)]
        public double FavMomoAmplifier { get; set; }

        // Quality gating and sizing
        [NinjaScriptProperty]
        [Display(Name = "UseQualityGate", GroupName = "Misc / Logging", Order = 20)]
        public bool UseQualityGate { get; set; }

        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "MinQTotal2", GroupName = "Misc / Logging", Order = 21)]
        public double MinQTotal2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "UseVolumeProfile", GroupName = "VP", Order = 22)]
        public bool UseVolumeProfile { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ApplyRunnerManagement", GroupName = "Runner", Order = 23)]
        public bool ApplyRunnerManagement { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "BatchTag", GroupName = "Misc / Logging", Order = 30)]
        public string BatchTag { get; set; }

        // Basic spacing and sizing parameters

		[NinjaScriptProperty][Range(0.0, 10.0)]
		[Display(Name = "MinSpaceR", GroupName = "Entry Filters", Order = 11)]
		public double MinSpaceR { get; set; }
		[NinjaScriptProperty][Range(0, 100)]
		[Display(Name = "MinAbsSpaceTicks", GroupName = "Entry Filters", Order = 12)]
		public int MinAbsSpaceTicks { get; set; }

        // Entry EMA anchor parameters
        // EntryEmaPeriod: period of the EMA used as the anchor for entry triggers (typically 62)
        // MinEmaTouchTicks: minimum number of ticks that price must close beyond the EMA after touching it
        [NinjaScriptProperty][Range(1, 200)]
        [Display(Name = "EntryEmaPeriod", GroupName = "Entry Filters", Order = 13)]
        public int EntryEmaPeriod { get; set; }

        [NinjaScriptProperty][Range(0, 100)]
        [Display(Name = "MinEmaTouchTicks", GroupName = "Entry Filters", Order = 14)]
        public int MinEmaTouchTicks { get; set; }

        // Toggles for advanced components
        [NinjaScriptProperty]
        [Display(Name = "UseMomentumCore", GroupName = "MomentumCore", Order = 50)]
        public bool UseMomentumCore { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "UsePosVolNodes", GroupName = "PosVol", Order = 51)]
        public bool UsePosVolNodes { get; set; }

        // PosVol node parameters
        [NinjaScriptProperty][Range(1, 100)]
        [Display(Name = "PosVol_RB_N", GroupName = "PosVol", Order = 52)]
        public int PosVol_RB_N { get; set; }
        [NinjaScriptProperty][Range(1, 10)]
        [Display(Name = "PosVol_LTF_K", GroupName = "PosVol", Order = 53)]
        public int PosVol_LTF_K { get; set; }
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "PosVol_InfluenceAlpha", GroupName = "PosVol", Order = 54)]
        public double PosVol_InfluenceAlpha { get; set; }
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "PosVol_InfluenceBeta", GroupName = "PosVol", Order = 55)]
        public double PosVol_InfluenceBeta { get; set; }
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "PosVol_InfluenceGamma", GroupName = "PosVol", Order = 56)]
        public double PosVol_InfluenceGamma { get; set; }

        // Momentum core parameter properties (not present in original file but required for compilation)
        [NinjaScriptProperty][Range(1, 200)]
        [Display(Name = "Momo_ATR_Period", GroupName = "MomentumCore", Order = 101)]
        public int Momo_ATR_Period { get; set; }
        [NinjaScriptProperty][Range(1, 200)]
        [Display(Name = "Momo_ADX_Len", GroupName = "MomentumCore", Order = 102)]
        public int Momo_ADX_Len { get; set; }
        [NinjaScriptProperty][Range(1, 200)]
        [Display(Name = "Momo_ROC_Lookback", GroupName = "MomentumCore", Order = 103)]
        public int Momo_ROC_Lookback { get; set; }
        [NinjaScriptProperty][Range(1, 200)]
        [Display(Name = "Momo_Z_Lookback", GroupName = "MomentumCore", Order = 104)]
        public int Momo_Z_Lookback { get; set; }
        [NinjaScriptProperty][Range(1, 200)]
        [Display(Name = "TSI_r", GroupName = "MomentumCore", Order = 105)]
        public int TSI_r { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "MACD_UseDEMA", GroupName = "MomentumCore", Order = 106)]
        public bool MACD_UseDEMA { get; set; }
        [NinjaScriptProperty][Range(1, 200)]
        [Display(Name = "MACD_fast", GroupName = "MomentumCore", Order = 107)]
        public int MACD_fast { get; set; }
        [NinjaScriptProperty][Range(1, 200)]
        [Display(Name = "MACD_slow", GroupName = "MomentumCore", Order = 108)]
        public int MACD_slow { get; set; }
        [NinjaScriptProperty][Range(1, 200)]
        [Display(Name = "MACD_signal", GroupName = "MomentumCore", Order = 109)]
        public int MACD_signal { get; set; }
        [NinjaScriptProperty][Range(1, 200)]
        [Display(Name = "ER_Len", GroupName = "MomentumCore", Order = 110)]
        public int ER_Len { get; set; }
        [NinjaScriptProperty][Range(1, 200)]
        [Display(Name = "Streak_N", GroupName = "MomentumCore", Order = 111)]
        public int Streak_N { get; set; }

        // Volume profile runner adjustment parameters
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "VP_RunnerK1", GroupName = "VP", Order = 200)]
        public double VP_RunnerK1Param { get; set; }
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "VP_RunnerK2", GroupName = "VP", Order = 201)]
        public double VP_RunnerK2Param { get; set; }
        [NinjaScriptProperty][Range(1, 100)]
        [Display(Name = "HysteresisBars", GroupName = "VP", Order = 202)]
        public int HysteresisBarsParam { get; set; }

[NinjaScriptProperty][Range(1, 100)]
[Display(Name="BaseContracts", GroupName="Position Settings", Order=1)]
public int BaseContracts { get; set; }


		[NinjaScriptProperty]
		[Display(Name="ForceEntry", GroupName="Debug", Order=999)]
		public bool ForceEntry { get; set; }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                // Always use OnBarClose for safety
                Description = "MNQRSTest strategy (PosVol and Momo logic added)";
                Name        = "MNQRSTest";
                Calculate   = Calculate.OnBarClose;

                // Fix default quantity at 1 (required by NinjaTrader)
                DefaultQuantity = 1;
                // BaseContracts is the user‑facing sizing parameter; ensure at least 1
                if (BaseContracts <= 0) BaseContracts = 1;

                // --- Default values for all public properties ---
                W_PosVolProxy = 0.0;
                W_FavMomo     = 0.0;
                W_TrueMomo    = 0.0;
                W_MomoROC = W_MomoTSI = W_MomoMACD = W_MomoER = W_MomoStreak = 0.0;
                W_PosVolVP    = 0.0;
                W_PosRes      = 0.0;
                FavMomoAmplifier = 0.0;
                UseQualityGate = false;
                MinQTotal2     = 0.5;
                UseVolumeProfile = false;
                ApplyRunnerManagement = false;
                BatchTag       = string.Empty;
                MinSpaceR      = 1.0;
                MinAbsSpaceTicks = 8;

                // Entry filter defaults
                UseEntryTimeFilter = false;
                EntryStartHour   = 9;
                EntryStartMinute = 30;
                EntryEndHour     = 15;
                EntryEndMinute   = 30;
                UseVolatilityFilter = false;
                MinATR          = 0.0;
                MaxATR          = 1000.0;
                UseTrendFilter  = false;
                TrendSlopeMin   = 0.0;

                // Runner gating thresholds
                RunnerMomoThreshold  = 0.0;
                RunnerSpaceThreshold = 0.0;

                // EMA anchor defaults
                EntryEmaPeriod   = 62;
                MinEmaTouchTicks = 2;

                // Advanced toggles defaults
                UseMomentumCore = false;
                UsePosVolNodes  = false;
                PosVol_RB_N     = 20;
                PosVol_LTF_K    = 2;
                PosVol_InfluenceAlpha = 0.0;
                PosVol_InfluenceBeta  = 0.0;
                PosVol_InfluenceGamma = 0.0;

                // VP runner adjustment defaults
                VP_RunnerK1Param = 0.0;
                VP_RunnerK2Param = 0.0;
                HysteresisBarsParam = 0;

                // Debug toggles
                ForceEntry = false;
            }
            else if (State == State.DataLoaded)
            {
                // Initialize indicators once data is loaded
                deltaStats = new RollingStats(100);
                dirEma     = new Ema(8);
                deltaEma   = new Ema(14);
                // Runner percentage base (0.5 if BaseContracts >= 2)
                lastRunnerPct = (BaseContracts < 2) ? 0.0 : 0.5;
                lastRunnerBasePct = lastRunnerPct;

                rsiIndicator = RSI(14, 3);
                adxIndicator = ADX(14);
                // Initialize EMA and ATR based on user‑configurable periods
                priceEma     = EMA(EntryEmaPeriod);
                atrIndicator = ATR(14);

                // Initialize momentum core if present
                try { MomentumCore_OnStateChange(); } catch { }

                // Initialize logging (creates files with headers)
                EnsureLogsInitialized();

                // Apply batch tag presets (A/B/C etc.) if set
                if (!string.IsNullOrEmpty(BatchTag))
                {
                    switch (BatchTag.ToUpper().Trim())
                    {
                        case "A":
                            // Batch A disables PosVol and momentum contributions
                            W_PosVolProxy = 0.0;
                            W_FavMomo     = 0.0;
                            W_TrueMomo    = 0.0;
                            W_MomoROC = W_MomoTSI = W_MomoMACD = W_MomoER = W_MomoStreak = 0.0;
                            UseVolumeProfile     = false;
                            ApplyRunnerManagement = false;
                            break;
                        // Additional batch tags (B/C/D) can be added here as needed
                    }
                }
            }
        }
    }
}