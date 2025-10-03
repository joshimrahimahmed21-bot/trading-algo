--- a/Strategy_files/MNQRSTest_Config.cs
+++ b/Strategy_files/MNQRSTest_Config.cs
@@
                   PosVol_InfluenceGamma = 0.0;
                   ForceEntry = false;
-                 if (State == State.SetDefaults)
-                 {
-                     Description = "MNQRSTest strategy (PosVol and Momo logic added)";
-                     Name = "MNQRSTest";
-                     Calculate = Calculate.OnBarClose;
-                     // Set default values for all public properties (removed legacy DefaultQuantityParam logic)
-                     RunnerMomoThreshold = 0.0;
-                     RunnerSpaceThreshold = 0.0;
-                     // Entry filter defaults
-                     UseEntryTimeFilter = false;
-                     EntryStartHour = 9;
-                     EntryStartMinute = 30;
-                     EntryEndHour = 15;
-                     EntryEndMinute = 30;
-                     UseVolatilityFilter = false;
-                     MinATR = 0.0;
-                     MaxATR = 1000.0;
-                     UseTrendFilter = false;
-                     TrendSlopeMin = 0.0;
-                 }
+                 // Position and sizing defaults
+                 BaseContracts = 1;
+                 DefaultQuantity = 1;
+                 // Runner threshold defaults
+                 RunnerMomoThreshold = 0.0;
+                 RunnerSpaceThreshold = 0.0;
+                 // Entry filter defaults
+                 UseEntryTimeFilter = false;
+                 EntryStartHour = 9;
+                 EntryStartMinute = 30;
+                 EntryEndHour = 15;
+                 EntryEndMinute = 30;
+                 UseVolatilityFilter = false;
+                 MinATR = 0.0;
+                 MaxATR = 1000.0;
+                 UseTrendFilter = false;
+                 TrendSlopeMin = 0.0;
+             }
               else if (State == State.DataLoaded)
               {
                   // Initialize indicators
                   deltaStats = new RollingStats(100);
@@
       deltaEma = new Ema(14);
-      lastRunnerPct = (DefaultQuantity < 2) ? 0.0 : 0.5;
+      lastRunnerPct = (BaseContracts < 2) ? 0.0 : 0.5;
       rsiIndicator = RSI(14, 3);
