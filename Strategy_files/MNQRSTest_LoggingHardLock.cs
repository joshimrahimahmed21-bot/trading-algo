--- a/Strategy_files/MNQRSTest_LoggingHardLock.cs
+++ b/Strategy_files/MNQRSTest_LoggingHardLock.cs
@@
-             info += $"DefaultQuantity: {DefaultQuantity}\n";
+             info += $"BaseContracts: {BaseContracts}\n";
@@
-    "Date,Time,Side,Entry,Stop,Target,Qty,EntryName,Q_Total2,Q_Swing,Q_Momo,Q_Vol,Q_Session,Q_ResRunner,SpaceR,ATR,SessionWeight\n"
+    "Date,Time,Side,Entry,Stop,Target,Qty,Contracts,EntryName,Q_Total2,Q_Swing,Q_Momo,Q_Vol,Q_Session,Q_ResRunner,SpaceR,ATR,SessionWeight\n"
@@
-             "{0},{1},{2},{3:F2},{4:F2},{5:F2},{6},{7},{8:F4},{9:F4},{10:F4},{11:F4},{12:F4},{13:F4},{14:F4},{15:F4},{16:F4}",
+             "{0},{1},{2},{3:F2},{4:F2},{5:F2},{6},{7},{8},{9:F4},{10:F4},{11:F4},{12:F4},{13:F4},{14:F4},{15:F4},{16:F4},{17:F4}",
@@
              targetPrice,
              qty,
+             BaseContracts,
              entryName,
