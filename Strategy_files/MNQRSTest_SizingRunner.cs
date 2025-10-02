using System;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class MNQRSTest : Strategy
    {
        private void EnsureSplitSizingReady()
        {
            if (!ApplyRunnerManagement)
            {
                lastRunnerPct = (DefaultQuantity < 2) ? 0.0 : 0.5;
                return;
            }
            // simplified risk-based sizing stub
            lastRunnerPct = (DefaultQuantity < 2) ? 0.0 : 0.5;
        }

        private void ApplyRunnerPreset(bool isLong)
        {
            int totalQty = (int)DefaultQuantity;
            if (totalQty <= 0) totalQty = 1;
            int runnerQty = (int)Math.Floor(totalQty * lastRunnerPct);
            if (runnerQty < 0) runnerQty = 0;
            if (runnerQty > totalQty) runnerQty = totalQty;
            int coreQty = totalQty - runnerQty;

            if (isLong)
            {
                if (coreQty > 0) EnterLong(coreQty, "CORE");
                if (runnerQty > 0) EnterLong(runnerQty, "RUNNER");
            }
            else
            {
                if (coreQty > 0) EnterShort(coreQty, "CORE");
                if (runnerQty > 0) EnterShort(runnerQty, "RUNNER");
            }
        }
    }
}
