using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Simplified logging stub for compile safety. 
    /// </summary>
    public partial class MNQRSTest : Strategy
    {
        private void EnsureLogsInitialized()
        {
            logsReady = true;
        }

        private void LogSetupRow(string evt, string side, string why,
                                 double trig, double stp, double tgt, double r,
                                 double stopTicks, string notes = "")
        {
            // no-op for compile safety
        }

        private void WriteDebugSetup(string tag, string notes)
        {
            // no-op
        }
    }

    public class TradeRow
    {
        public DateTime Time { get; set; }
        public string Side { get; set; }
        public double Entry { get; set; }
        public double Stop { get; set; }
        public double Target { get; set; }
        public int Qty { get; set; }
        public double Exit { get; set; }
        public string ExitType { get; set; }
    }
}
