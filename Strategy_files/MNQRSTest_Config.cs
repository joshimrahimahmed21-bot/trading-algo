// using System;
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

        // Positional volume proxy values handled in momentum partial

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
        private string tradeLogPath;
        private string setupLogPath;

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
        [Display(Name = "UseQualityGate", GroupName = "Parameters", Order = 20)]
        public bool UseQualityGate { get; set; }

        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "MinQTotal2", GroupName = "Parameters", Order = 21)]
        public double MinQTotal2 { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "UseVolumeProfile", GroupName = "Parameters", Order = 22)]
        public bool UseVolumeProfile { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ApplyRunnerManagement", GroupName = "Parameters", Order = 23)]
        public bool ApplyRunnerManagement { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "BatchTag", GroupName = "Parameters", Order = 30)]
        public string BatchTag { get; set; }

        // Basic spacing and sizing parameters
        [NinjaScriptProperty]
        [Display(Name = "MinSpaceR", GroupName = "Parameters", Order = 40)]
        public double MinSpaceR { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "MinAbsSpaceTicks", GroupName = "Parameters", Order = 41)]
        public int MinAbsSpaceTicks { get; set; }

        // Toggles for advanced components
        [NinjaScriptProperty]
        [Display(Name = "UseMomentumCore", GroupName = "Parameters", Order = 50)]
        public bool UseMomentumCore { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "UsePosVolNodes", GroupName = "Parameters", Order = 51)]
        public bool UsePosVolNodes { get; set; }

        // PosVol node parameters
        [NinjaScriptProperty][Range(1, 100)]
        [Display(Name = "PosVol_RB_N", GroupName = "Parameters", Order = 52)]
        public int PosVol_RB_N { get; set; }
        [NinjaScriptProperty][Range(1, 10)]
        [Display(Name = "PosVol_LTF_K", GroupName = "Parameters", Order = 53)]
        public int PosVol_LTF_K { get; set; }
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "PosVol_InfluenceAlpha", GroupName = "Parameters", Order = 54)]
        public double PosVol_InfluenceAlpha { get; set; }
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "PosVol_InfluenceBeta", GroupName = "Parameters", Order = 55)]
        public double PosVol_InfluenceBeta { get; set; }
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "PosVol_InfluenceGamma", GroupName = "Parameters", Order = 56)]
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

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "MNQRSTest strategy (PosVol and Momo logic added)";
                Name = "MNQRSTest";
                Calculate = Calculate.OnBarClose;
                // Set default values for all public properties
                W_PosVolProxy = 0.0;
                W_FavMomo = 0.0;
                W_TrueMomo = 0.0;
                W_MomoROC = W_MomoTSI = W_MomoMACD = W_MomoER = W_MomoStreak = 0.0;
                W_PosVolVP = 0.0;
                W_PosRes = 0.0;
                FavMomoAmplifier = 0.0;
                UseQualityGate = false;
                MinQTotal2 = 0.5;
                UseVolumeProfile = false;
                ApplyRunnerManagement = false;
                BatchTag = string.Empty;
                MinSpaceR = 1.0;
                MinAbsSpaceTicks = 8;
                UseMomentumCore = false;
                UsePosVolNodes = false;
                PosVol_RB_N = 20;
                PosVol_LTF_K = 2;
                PosVol_InfluenceAlpha = 0.0;
                PosVol_InfluenceBeta = 0.0;
                PosVol_InfluenceGamma = 0.0;
                // Session defaults
                UseSessionAnchor = false;
                AnchorHour = 0;
                AnchorMinute = 0;
                SessionWindowShape = "Gaussian";
                PreScale = 1.0;
                PostScale = 1.0;
                AnchorWindowMins = 240.0;
                // Default session centers mode to Off to avoid null values
                SessionCentersModeParamV2 = SessionCentersMode.Off;
                // Momentum core defaults
                Momo_ATR_Period = 14;
                Momo_ADX_Len = 14;
                Momo_ROC_Lookback = 10;
                Momo_Z_Lookback = 20;
                TSI_r = 14;
                MACD_UseDEMA = false;
                MACD_fast = 12;
                MACD_slow = 26;
                MACD_signal = 9;
                ER_Len = 14;
                Streak_N = 10;
                // VP defaults
                VP_RunnerK1Param = 0.1;
                VP_RunnerK2Param = 0.1;
                HysteresisBarsParam = 10;
                // Runner defaults
                SetDefaultQuantity(1);
            }
            else if (State == State.Configure)
            {
                // (Add any secondary bar series if needed for multi-timeframe logic – not used here)
            }
            else if (State == State.DataLoaded)
            {
                // Initialize indicator and utility objects
                deltaStats = new RollingStats(100);
                dirEma = new Ema(8);
                deltaEma = new Ema(14);
                // Set initial runner percentage based on quantity (0 if only 1 contract, 0.5 if 2+)            
                lastRunnerPct = (DefaultQuantity < 2) ? 0.0 : 0.5;
                // Instantiate any indicators needed
                rsiIndicator = RSI(14, 3);
                adxIndicator = ADX(14);
                // Initialize momentum-core dependent components
                try
                {
                    MomentumCore_OnStateChange();
                }
                catch {}
                // Apply BatchTag presets for A/B/C/D scenarios
                if (!string.IsNullOrEmpty(BatchTag))
                {
                    switch (BatchTag.ToUpper().Trim())
                    {
                        case "A":
                            W_PosVolProxy = W_FavMomo = W_TrueMomo = 0.0;
                            W_MomoROC = W_MomoTSI = W_MomoMACD = W_MomoER = W_MomoStreak = 0.0;
                            UseVolumeProfile = false;
                            ApplyRunnerManagement = false;
                            break;
                        case "B":
                            W_PosVolProxy = 0.8;
                            W_FavMomo = 0.0;
                            W_TrueMomo = 1.0;
                            W_MomoROC = W_MomoTSI = W_MomoMACD = W_MomoER = W_MomoStreak = 0.0;
                            UseVolumeProfile = false;
                            ApplyRunnerManagement = false;
                            break;
                        case "C":
                            W_PosVolProxy = 0.0;
                            W_FavMomo = 0.0;
                            W_TrueMomo = 0.0;
                            W_MomoROC = 0.2;
                            W_MomoTSI = 0.4;
                            W_MomoMACD = 0.4;
                            W_MomoER = 0.0;
                            W_MomoStreak = 0.0;
                            UseVolumeProfile = false;
                            ApplyRunnerManagement = false;
                            break;
                        case "D":
                            W_PosVolProxy = 0.0;
                            W_FavMomo = 0.0;
                            W_TrueMomo = 1.0;
                            W_MomoROC = 0.2;
                            W_MomoTSI = 0.4;
                            W_MomoMACD = 0.4;
                            W_MomoER = 0.0;
                            W_MomoStreak = 0.0;
                            UseVolumeProfile = false;
                            ApplyRunnerManagement = false;
                            break;
                    }
                }
            }
        }
    }
}
