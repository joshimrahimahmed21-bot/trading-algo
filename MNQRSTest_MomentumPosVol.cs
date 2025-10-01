using System;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.Indicators;
using System.ComponentModel;


namespace NinjaTrader.NinjaScript.Strategies
    public partial class MNQRSTest : Strategy
    {
        // === Momentum Core (observe-only default) ===

        [Browsable(false)] public double lastQ_Momo_ROC     { get; private set; } = 0.5;
        [Browsable(false)] public double lastQ_Momo_TSI     { get; private set; } = 0.5;
        [Browsable(false)] public double lastQ_Momo_MACD    { get; private set; } = 0.5;
        [Browsable(false)] public double lastQ_Momo_ER      { get; private set; } = 0.5;
        [Browsable(false)] public double lastQ_Momo_Streak  { get; private set; } = 0.5;
        [Browsable(false)] public double lastQ_Momo_Core    { get; private set; } = 0.5;
        [Browsable(false)] public double lastMomoConf       { get; private set; } = 0.8;

        private ATR _atr;
        private ADX _adx;

        /// <summary>
        /// Squashes a z-score using a tunable divisor.  This mirrors the original
        /// static SquashZ(z, k) implementation but avoids name collisions by using a
        /// unique method name.
        /// </summary>
        private double SquashZ(double z, double k = 2.0)
        {
            return 0.5 * (Math.Tanh(z / k) + 1.0);
        }

        private void MomentumCore_OnStateChange()
        {
            if (State == State.DataLoaded)
            {
                _atr = ATR(Momo_ATR_Period);
                _adx = ADX(Momo_ADX_Len);
            }
        }



        private double ZScoreSeries(Func<int,double> series, int lookback)
        {
            int n = Math.Min(CurrentBar, lookback);
            if (n <= 3) return 0.0;
            double sum=0.0;
            for (int i=0;i<n;i++) sum += series(i);
            double mean = sum / n;
            double ss=0.0;
            for (int i=0;i<n;i++){ double v=series(i)-mean; ss += v*v; }
            double std = Math.Sqrt(Math.Max(1e-9, ss/Math.Max(1,n-1)));
            double cur = series(0);
            return (cur - mean) / (std>0?std:1.0);
        }

        private double EMA1(Func<int,double> s, int len)
        {
            int n = Math.Min(CurrentBar, Math.Max(1, len));
            double k = 2.0 / (len + 1.0);
            double ema = s(n-1);
            for (int i=n-2;i>=0;i--) ema = k*s(i) + (1-k)*ema;
            return ema;
        }

        private double DEMA1(Func<int,double> s, int len)
        {
            double ema1 = EMA1(s, len);
            Func<int,double> s2 = (i) => EMA1(s, len);
            double ema2 = EMA1(s2, len);
            return 2*ema1 - ema2;
        }

        public void MomentumCore_Update(bool isLong, out double qMomoCore)
        {
            if (!UseMomentumCore)
            {
                qMomoCore = 0.5;
                lastQ_Momo_Core = 0.5;
                lastMomoConf = 0.8;
                return;
            }

            // --- ROC ---
            int nROC = Math.Max(1, Momo_ROC_Lookback);
            double roc = (Close[0] - (CurrentBar > nROC ? Close[nROC] : Close[0])) / Math.Max(1e-9, _atr[0]);
            double zROC = ZScoreSeries(i => (Close[i] - (i+nROC<Closes[0].Count?Close[i+nROC]:Close[i])) / Math.Max(1e-9,_atr[i]), Momo_Z_Lookback);
            lastQ_Momo_ROC = isLong ? SquashZ(zROC) : 1.0 - SquashZ(zROC);

            // --- TSI ---
            Func<int,double> d = i => Close[i] - (i+1<Closes[0].Count?Close[i+1]:Close[i]);
            Func<int,double> ad = i => Math.Abs(d(i));
            double num = EMA1(d, TSI_r);
            double den = Math.Max(1e-9, EMA1(ad, TSI_r));
            double tsi = num/den;
            lastQ_Momo_TSI = Clamp01(isLong ? (0.5*(tsi+1)) : 1-(0.5*(tsi+1)));

            // --- MACD ---
            Func<int,double> src = i => Close[i];
            double emaF = MACD_UseDEMA? DEMA1(src, MACD_fast):EMA1(src,MACD_fast);
            double emaS = MACD_UseDEMA? DEMA1(src, MACD_slow):EMA1(src,MACD_slow);
            double macd = emaF - emaS;
            double sig  = EMA1(i=>emaF-emaS, MACD_signal);
            double hist = macd - sig;
            double mMACD = SquashZ(hist/Math.Max(1e-9,1.5*_atr[0]));
            lastQ_Momo_MACD = Clamp01(isLong?mMACD:1-mMACD);

            // --- ER ---
            int erN = Math.Max(2, ER_Len);
            double dir = Math.Abs(Close[0] - (CurrentBar>erN?Close[erN]:Close[0]));
            double volPath=0; for(int i=1;i<=erN && i<CurrentBar;i++) volPath += Math.Abs(Close[i-1]-Close[i]);
            double ER = dir/Math.Max(1e-9,volPath);
            double zER = ZScoreSeries(i=>ER, Momo_Z_Lookback);
            double mER = SquashZ(zER);
            lastQ_Momo_ER = Clamp01(isLong?mER:1-mER);

            // --- Streak ---
            int N = Math.Max(1, Streak_N);
            double eSum=0;
            for(int i=0;i<N && i<CurrentBar;i++)
            {
                double bodyPct=(High[i]-Low[i])>0?Math.Abs(Close[i]-Open[i])/(High[i]-Low[i]):0;
                double sgn=(Close[i]>Open[i])?1:(Close[i]<Open[i]?-1:0);
                eSum+=sgn*bodyPct;
            }
            double zSE=ZScoreSeries(i=>{
                double bp=(High[i]-Low[i])>0?Math.Abs(Close[i]-Open[i])/(High[i]-Low[i]):0;
                double sgn=(Close[i]>Open[i])?1:(Close[i]<Open[i]?-1:0);
                return sgn*bp;
            },Momo_Z_Lookback);
            double mSE=SquashZ(zSE);
            lastQ_Momo_Streak=Clamp01(isLong?mSE:1-mSE);

            // --- Composite ---
            double numw=0,denw=0;
            void add(double w,double v){numw+=w*v;denw+=w;}
            add(W_MomoROC,lastQ_Momo_ROC);
            add(W_MomoTSI,lastQ_Momo_TSI);
            add(W_MomoMACD,lastQ_Momo_MACD);
            add(W_MomoER,lastQ_Momo_ER);
            add(W_MomoStreak,lastQ_Momo_Streak);
            lastQ_Momo_Core=denw>0?Clamp01(numw/denw):0.5;

            // --- Confidence ---
            double adxN=Math.Min(1.0,_adx[0]/50.0);
            lastMomoConf=Clamp01(adxN);

            qMomoCore=lastQ_Momo_Core;
        }
    }
}


    public partial class MNQRSTest : Strategy
    {
        // Momentum Families (Fav/True) computed from base momentum and positional volume bias.

        [Browsable(false)] public double lastFavMomo { get; private set; } = 0.5;
        [Browsable(false)] public double lastTrueMomo { get; private set; } = 0.5;

        /// <summary>
        /// Update FavMomo and TrueMomo based on current space/trend quality and volume bias.
        /// </summary>
        /// <param name="qSpace">Current space (congestion) quality in 0..1</param>
        /// <param name="qTrend">Current trend quality in 0..1</param>
        private void MomoFamilies_Update(double qSpace, double qTrend)
        {
            // Ensure PosVol proxy values are current if enabled
            if (UsePosVolNodes)
                PosVol_UpdateInline();

            // Determine base momentum: if any momentum-core weights are non-zero, use the composite core value;
            // otherwise fall back to the legacy momentum proxy (Q_Momo).
            double totalCoreW = W_MomoROC + W_MomoTSI + W_MomoMACD + W_MomoER + W_MomoStreak;
            double baseMomo = (totalCoreW > 1e-6 ? lastQ_Momo_Core : Q_Momo);

            // Favored momentum: amplify by directional volume bias.
            double fav = baseMomo;
            double lambda = FavMomoAmplifier;
            if (lambda != 0.0)
                fav = baseMomo * (1.0 + lambda * (lastQ_PosVol_Proxy - 0.5));
            fav = Clamp01(fav);

            // True momentum: blend base and fav based on congestion/trend context.
            // More congestion or weaker trend implies larger weight on favoured momentum.
            double alpha = 1.0 - Clamp01(0.5 * qSpace + 0.5 * qTrend);
            double trueM = alpha * baseMomo + (1.0 - alpha) * fav;
            trueM = Clamp01(trueM);

            lastFavMomo = fav;
            lastTrueMomo = trueM;
        }
    }
}

    public partial class MNQRSTest : Strategy
    {
        // =========================
        // PosVol Proxy Node System (helper-driven, NO overrides here)
        // =========================

        [Browsable(false)] public double lastQ_PosVol_RB { get; private set; } = 0.5;
        [Browsable(false)] public double lastQ_PosVol_SB { get; private set; } = 0.5;
        [Browsable(false)] public double lastQ_PosVol_LTF { get; private set; } = 0.5;
        [Browsable(false)] public double lastQ_PosVol_Proxy { get; private set; } = 0.5;
        [Browsable(false)] public double lastQ_PosVol_Proxy_Conf { get; private set; } = 0.8;

        private NodeGraph _posVolGraph;

        // Call this from quality/momo path before reading lastQ_* values
        private void PosVol_UpdateInline()
        {
            if (!UsePosVolNodes)
                return;

            if (_posVolGraph == null)
                _posVolGraph = new NodeGraph(this);

            // Update primary timeframe nodes
            _posVolGraph.UpdatePrimary();
            // Try LTF if a 1-minute series is present; otherwise RB/SB carry most of the weight
            if (Closes != null && Closes.Count > 1)
                _posVolGraph.UpdateLTF();

            lastQ_PosVol_RB = _posVolGraph.RB_Value;
            lastQ_PosVol_SB = _posVolGraph.SB_Value;
            lastQ_PosVol_LTF = _posVolGraph.LTF_Value;
            lastQ_PosVol_Proxy = _posVolGraph.Proxy_Value;
            lastQ_PosVol_Proxy_Conf = _posVolGraph.Proxy_Conf;
        }

        private class NodeGraph
        {
            private readonly MNQRSTest _s;
            private readonly RBNode _rb;
            private readonly SBNode _sb;
            private readonly LTFNode _ltf;

            public double RB_Value { get; private set; } = 0.5;
            public double SB_Value { get; private set; } = 0.5;
            public double LTF_Value { get; private set; } = 0.5;
            public double Proxy_Value { get; private set; } = 0.5;
            public double Proxy_Conf { get; private set; } = 0.8;

            public NodeGraph(MNQRSTest s)
            {
                _s = s;
                _rb = new RBNode(s);
                _sb = new SBNode(s);
                _ltf = new LTFNode(s);
            }

            public void UpdateLTF()
            {
                _ltf.Update();
                LTF_Value = _ltf.Value;
            }

            public void UpdatePrimary()
            {
                _rb.Update();
                _sb.Update();

                RB_Value = _rb.Value;
                SB_Value = _sb.Value;

                // Influence model
                double alpha = _s.PosVol_InfluenceAlpha;
                double beta  = _s.PosVol_InfluenceBeta;
                double gamma = _s.PosVol_InfluenceGamma;

                // If no LTF sample yet, use neutral 0.5 / 0.7 conf
                double ltfV = _ltf.Value;
                double ltfC = _ltf.Conf;

                double RBp = PVClamp01(RB_Value + alpha * (ltfV - 0.5) * ltfC);
                double RBpp = (1.0 - beta * _sb.Conf) * RBp + (beta * _sb.Conf) * SB_Value;

                double conflict = Math.Max(0.0, Math.Abs(RBpp - SB_Value) - 0.25);
                double baseConf = 0.8;
                double conf = PVClamp01(baseConf * (1.0 - gamma * conflict));
                double ltfBoost = 1.0 + 0.1 * (ltfC - 0.5);

                Proxy_Value = RBpp;
                Proxy_Conf = PVClamp01(conf * ltfBoost);
            }
        }

        private static double PVClamp01(double x) => Math.Min(1.0, Math.Max(0.0, x));

        private abstract class PosVolNodeBase
        {
            protected readonly MNQRSTest S;
            public double Value { get; protected set; } = 0.5;
            public double Conf  { get; protected set; } = 0.8;
            public PosVolNodeBase(MNQRSTest s) { S = s; }
            public abstract void Update();
        }

        private class RBNode : PosVolNodeBase
        {
            private int _N => S.PosVol_RB_N;
            private double _zDiv = 2.0;

            public RBNode(MNQRSTest s) : base(s) {}

            public override void Update()
            {
                double sum = 0.0;
                int count = 0;
                int n = Math.Min(_N, S.CurrentBar);
                for (int i = 0; i < n; i++)
                {
                    double vol = S.Volume[i];
                    double dir = (S.Close[i] > S.Open[i]) ? 1.0 : (S.Close[i] < S.Open[i]) ? -1.0 : 0.0;
                    double body = Math.Abs(S.Close[i] - S.Open[i]);
                    double tr = Math.Max(1e-9, S.High[i] - S.Low[i]);
                    double bodyPct = tr > 0 ? (body / tr) : 0.0;
                    double signedVol = vol * dir * (0.6 + 0.4 * bodyPct);
                    sum += signedVol;
                    count++;
                }
                if (count <= 2)
                {
                    Value = 0.5; Conf = 0.2; return;
                }

                // crude z on the aggregated sum
                double mean = sum / Math.Max(1, count);
                double std = Math.Max(1.0, Math.Sqrt(Math.Abs(mean))); // keep bounded
                double z = (sum - mean) / std;
                Value = 0.5 * (Math.Tanh(z / _zDiv) + 1.0);
                Conf  = 0.7;
            }
        }

        private class SBNode : PosVolNodeBase
        {
            public SBNode(MNQRSTest s) : base(s) { }
            public override void Update()
            {
                int n = Math.Min(50, S.CurrentBar);
                if (n < 5) { Value = 0.5; Conf = 0.3; return; }

                int pivotLook = 3;
                int lastPivot = -1;
                for (int i = pivotLook; i < Math.Min(S.CurrentBar - pivotLook, 200); i++)
                {
                    bool isHigh = true, isLow = true;
                    for (int k = 1; k <= pivotLook; k++)
                    {
                        if (S.High[i] <= S.High[i - k] || S.High[i] <= S.High[i + k]) isHigh = false;
                        if (S.Low[i]  >= S.Low[i - k]  || S.Low[i]  >= S.Low[i + k])  isLow = false;
                        if (!isHigh && !isLow) break;
                    }
                    if (isHigh || isLow)
                        lastPivot = i;
                }
                if (lastPivot < 0 || lastPivot > S.CurrentBar - 2)
                {
                    Value = 0.5; Conf = 0.3; return;
                }

                double sum = 0.0;
                int span = Math.Min(100, S.CurrentBar - lastPivot);
                for (int i = 0; i < span; i++)
                {
                    double vol = S.Volume[i];
                    double dir = (S.Close[i] > S.Open[i]) ? 1.0 : (S.Close[i] < S.Open[i]) ? -1.0 : 0.0;
                    sum += vol * dir;
                }

                double z = sum / Math.Max(1.0, 1000.0);
                Value = 0.5 * (Math.Tanh(z) + 1.0);
                Conf = 0.85;
            }
        }

        private class LTFNode : PosVolNodeBase
        {
            private int _K => Math.Max(1, Math.Min(3, S.PosVol_LTF_K));

            public LTFNode(MNQRSTest s) : base(s) { }

            public override void Update()
            {
                if (S.Closes == null || S.Closes.Count < 2) { Value = 0.5; Conf = 0.7; return; }

                double sum = 0.0;
                int bars = Math.Min(_K, S.Closes[1].Count - 1);
                for (int j = 0; j < bars; j++)
                {
                    double c = S.Closes[1][j];
                    double o = S.Opens[1][j];
                    double v = S.Volumes[1][j];
                    double dir = (c > o) ? 1.0 : (c < o) ? -1.0 : 0.0;
                    sum += v * dir;
                }
                double z = sum / Math.Max(1.0, 1000.0);
                Value = 0.5 * (Math.Tanh(z) + 1.0);
                Conf  = 0.7;
            }