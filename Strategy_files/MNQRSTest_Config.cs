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
    public partial class MNQRSTest : Strategy
    {
        // Helper methods added by refactor
        private static double Clamp01(double x) => x < 0 ? 0 : (x > 1 ? 1 : x);
        private static double Squash(double z) => 0.5 * (System.Math.Tanh(z) + 1.0);
        private static double Blend(double a, double b, double w) => (1 - w) * a + w * b;

        // New weight properties (all 0–1 range)
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "W_PosVolProxy", GroupName = "Parameters", Order = 1)]
        public double W_PosVolProxy { get; set; }
        
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "W_FavMomo", GroupName = "Parameters", Order = 2)]
        public double W_FavMomo { get; set; }
        
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "W_TrueMomo", GroupName = "Parameters", Order = 3)]
        public double W_TrueMomo { get; set; }
        
        // MomentumCore node weights
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
        
        // Other new weights
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "W_PosVolVP", GroupName = "Parameters", Order = 4)]
        public double W_PosVolVP { get; set; }
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "W_PosRes", GroupName = "Parameters", Order = 5)]
        public double W_PosRes { get; set; }
        
        // FavMomo amplification factor (λ)
        [NinjaScriptProperty][Range(0.0, 1.0)]
        [Display(Name = "FavMomoAmplifier", GroupName = "Parameters", Order = 6)]
        public double FavMomoAmplifier { get; set; }
        
        // Feature toggles and BatchTag
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
        
        // Existing parameters (examples)
        [NinjaScriptProperty]
        [Display(Name = "MinSpaceR", GroupName = "Parameters", Order = 40)]
        public double MinSpaceR { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "MinAbsSpaceTicks", GroupName = "Parameters", Order = 41)]
        public int MinAbsSpaceTicks { get; set; }
        // === Additional feature toggles and PosVol/Momentum settings ===
        [NinjaScriptProperty]
        [Display(Name = "UseMomentumCore", GroupName = "Parameters", Order = 50)]
        public bool UseMomentumCore { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "UsePosVolNodes", GroupName = "Parameters", Order = 51)]
        public bool UsePosVolNodes { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "PosVol_RB_N", GroupName = "Parameters", Order = 52)]
        public int PosVol_RB_N { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "PosVol_LTF_K", GroupName = "Parameters", Order = 53)]
        public int PosVol_LTF_K { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 1.0)]
        [Display(Name = "PosVol_InfluenceAlpha", GroupName = "Parameters", Order = 54)]
        public double PosVol_InfluenceAlpha { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 1.0)]
        [Display(Name = "PosVol_InfluenceBeta", GroupName = "Parameters", Order = 55)]
        public double PosVol_InfluenceBeta { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 1.0)]
        [Display(Name = "PosVol_InfluenceGamma", GroupName = "Parameters", Order = 56)]
        public double PosVol_InfluenceGamma { get; set; }
        
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
                // Defaults for new PosVol and momentum feature toggles
                UseMomentumCore = false;
                UsePosVolNodes = false;
                PosVol_RB_N = 20;
                PosVol_LTF_K = 2;
                PosVol_InfluenceAlpha = 0.0;
                PosVol_InfluenceBeta = 0.0;
                PosVol_InfluenceGamma = 0.0;

                // Session anchor defaults
                UseSessionAnchor = false;
                AnchorHour = 0;
                AnchorMinute = 0;
                SessionWindowShape = "Gaussian";
                PreScale = 1.0;
                PostScale = 1.0;
                AnchorWindowMins = 240.0;
                SessionCentersModeParamV2 = SessionCentersMode.Off;
                SetDefaultQuantity(1);   // default order quantity
            }
            else if (State == State.Configure)
            {
                // (Add any secondary bar series if needed for multi-timeframe logic – not used here)
            }
            else if (State == State.DataLoaded)
            {
                // Initialize indicator and utility objects, runner percentage, etc.
                deltaStats = new RollingStats(100);
                dirEma = new Ema(8);
                deltaEma = new Ema(14);
                // Set initial runner percentage based on quantity (0 if only 1 contract, 0.5 default if 2+)
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
                            // All new features off (baseline)
                            W_PosVolProxy = W_FavMomo = W_TrueMomo = 0.0;
                            W_MomoROC = W_MomoTSI = W_MomoMACD = W_MomoER = W_MomoStreak = 0.0;
                            UseVolumeProfile = false;
                            ApplyRunnerManagement = false;
                            break;
                        case "B":
                            // PosVol only: PosVolProxy on, TrueMomo on (MomentumCore off, FavMomo off)
                            W_PosVolProxy = 0.8;
                            W_FavMomo = 0.0;
                            W_TrueMomo = 1.0;
                            W_MomoROC = W_MomoTSI = W_MomoMACD = W_MomoER = W_MomoStreak = 0.0;
                            UseVolumeProfile = false;
                            ApplyRunnerManagement = false;
                            break;
                        case "C":
                            // MomentumCore only: TSI/MACD heavy, PosVol off, TrueMomo off
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
                            // TrueMomo combo: like C plus TrueMomo on
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
