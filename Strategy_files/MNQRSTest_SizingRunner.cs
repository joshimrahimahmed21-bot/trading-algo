--- a/Strategy_files/MNQRSTest_SizingRunner.cs
+++ b/Strategy_files/MNQRSTest_SizingRunner.cs
@@
-            lastRunnerBasePct = (DefaultQuantity < 2 ? 0.0 : 0.5);
+            lastRunnerBasePct = (BaseContracts < 2 ? 0.0 : 0.5);
             lastRunnerPct = lastRunnerBasePct;
@@
       // compute normal allowRunner chain
-     bool allowRunner = ApplyRunnerManagement && DefaultQuantity >= 2;
+     bool allowRunner = ApplyRunnerManagement && BaseContracts >= 2;
       if (allowRunner)
       {
           allowRunner = (lastQRes >= RunnerSpaceThreshold) && (lastQMomoCore >= RunnerMomoThreshold);
       }
       // debug: print each factor so you see what's blocking
-     Print($"ApplyRunnerManagement={ApplyRunnerManagement}, DefaultQuantity={DefaultQuantity}, " +
+     Print($"ApplyRunnerManagement={ApplyRunnerManagement}, BaseContracts={BaseContracts}, " +
            $"QRes={lastQRes:F2} vs {RunnerSpaceThreshold}, QMomo={lastQMomoCore:F2} vs {RunnerMomoThreshold}, " +
            $"Result={allowRunner}");
@@
-     int qty = Math.Max(1, (int)DefaultQuantity);
+     int qty = Math.Max(1, BaseContracts);
      double entryPrice = Close[0];
@@
-        int runnerQty = Math.Max(1, qty / 2);
-        int coreQty   = Math.Max(1, qty - runnerQty);
+        int runnerQty = Math.Max(1, BaseContracts / 2);
+        int coreQty   = BaseContracts - runnerQty;
         // CORE: 1R stop + 1R target
         SetStopLoss     ("CORE",   CalculationMode.Price, stopPrice,  false);
         SetProfitTarget ("CORE",   CalculationMode.Price, targetPrice);
