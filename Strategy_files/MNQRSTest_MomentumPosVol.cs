using System;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Momentum and positional volume logic for the MNQRSTest strategy.  This
    /// partial implements a simplified momentum core update as well as
    /// favoured/true momentum families and a stub for the PosVol node system.
    /// The goal is to provide compile‑safe hooks for momentum processing
    /// without including the full original NodeGraph implementation.
    /// </summary>
    public partial class MNQRSTest : Strategy
    {
        /// <summary>
        /// Momentum core initialization invoked from OnStateChange().  In the compile-safe
        /// version this method performs no actions but exists to satisfy references
        /// from the Config partial.  Extend this method to initialize any momentum
        /// specific indicators or state when adding a full momentum core implementation.
        /// </summary>
        private void MomentumCore_OnStateChange()
        {
            // No initialization required for the simplified momentum core stub.
        }
        // Last computed core momentum value is declared in the Config partial (MNQRSTest_Config.cs).
        // Do not redeclare it here to avoid duplicate field definitions across partial files.
        // Favoured and true momentum values
        private double lastFavMomo;
        private double lastTrueMomo;

        /// <summary>
        /// Compute a core momentum quality value.  In this simplified
        /// implementation we derive momentum from the RSI‑based Q_Momo
        /// component and optionally invert it for short trades.  If
        /// UseMomentumCore is disabled the result defaults to 0.5.  The
        /// value is stored in lastQMomoCore for later use.
        /// </summary>
        private void MomentumCore_Update(bool isLong, out double qMomoCore)
        {
            if (!UseMomentumCore)
            {
                qMomoCore = 0.5;
                lastQMomoCore = 0.5;
                return;
            }
            // Use the RSI‑derived momentum proxy from entry quality
            double baseMomo = Q_Momo;
            if (!isLong)
            {
                // Invert momentum for short context
                baseMomo = 1.0 - baseMomo;
            }
            baseMomo = Helpers.Clamp01(baseMomo);
            lastQMomoCore = baseMomo;
            qMomoCore = baseMomo;
        }

        /// <summary>
        /// Update FavMomo and TrueMomo metrics.  FavMomo amplifies the
        /// base momentum by the positional volume bias via FavMomoAmplifier.
        /// TrueMomo blends base and favoured momentum depending on
        /// congestion/trend context: more congestion leads to greater
        /// emphasis on favoured momentum.  Results are clamped into
        /// [0,1] and stored in lastFavMomo and lastTrueMomo.
        /// </summary>
        private void MomoFamilies_Update(double qSpace, double qTrend)
        {
            // Select base momentum: use core value if momentum weights are specified
            double baseMomo = UseMomentumCore ? lastQMomoCore : Q_Momo;
            // Favoured momentum amplifies directional bias
            double fav = baseMomo;
            if (FavMomoAmplifier != 0.0)
            {
                fav = baseMomo * (1.0 + FavMomoAmplifier * (lastQ_PosVol_Proxy - 0.5));
                fav = Helpers.Clamp01(fav);
            }
            // True momentum: blend based on congestion/trend
            double alpha = 1.0 - Helpers.Clamp01(0.5 * qSpace + 0.5 * qTrend);
            double trueM = alpha * baseMomo + (1.0 - alpha) * fav;
            trueM = Helpers.Clamp01(trueM);
            lastFavMomo = fav;
            lastTrueMomo = trueM;
        }

        // PosVol node system stub values
        private double lastQ_PosVol_RB;
        private double lastQ_PosVol_SB;
        private double lastQ_PosVol_LTF;

        /// <summary>
        /// Update the PosVol node system.  The original implementation
        /// computes RB/SB/LTF node values and combines them via a NodeGraph.
        /// For compile safety we simply mirror the last positional volume
        /// proxy into the individual node values and confidence.  When
        /// UsePosVolNodes is disabled, this method does nothing.
        /// </summary>
        private void PosVol_UpdateInline()
        {
            if (!UsePosVolNodes)
                return;
            // Mirror the proxy into RB, SB and LTF values
            lastQ_PosVol_RB = lastQ_PosVol_Proxy;
            lastQ_PosVol_SB = lastQ_PosVol_Proxy;
            lastQ_PosVol_LTF = lastQ_PosVol_Proxy;
            lastQ_PosVol_Proxy_Conf = 0.8;
        }
    }
}