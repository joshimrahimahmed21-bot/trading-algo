# MNQRSTest Strategy (NinjaTrader 8)

## Overview
This repository contains the consolidated version of the **MNQRSTest** trading strategy for NinjaTrader 8, along with supporting utilities, documentation, and telemetry schema.  

The strategy implements:
- **Entry gating** via composite quality metrics (Q_Total2)
- **Momentum logic** (FavMomo / TrueMomo from volume + regime context)
- **Positional volume quality** (Q_PosVol)
- **Runner logic** for partial position management
- **Volume profile (VP) management** for trail/runner adjustments
- **Centralized telemetry** (CSV + HardLock) to ensure cross-engine parity with Python replicas

---

## Repository Layout

/Strategies/
├── MNQRSTest_Config.cs # Input parameters, defaults, shared fields
├── MNQRSTest_EntryQuality.cs # OnBarUpdate loop, quality gating, entry signals
├── MNQRSTest_MomentumPosVol.cs # MomentumCore + PosVol node graph
├── MNQRSTest_SessionVP.cs # Session anchor weighting, VP context, overlay
├── MNQRSTest_SizingRunner.cs # Risk-based sizing, runner split logic
├── MNQRSTest_LoggingHardLock.cs # Telemetry + CSV/HardLock exporters (to be unified)
├── MNQRSTest_Utilities.cs # Helpers (Clamp01, Squash, Blend, RollingStats, Ema)
└── QVP Indicator.cs # Volume profile indicator used by VP logic

/docs/
├── CODER_HANDBOOK.md # Compile-safe coder handbook (current coding standards)
├── Module Info For Parity.docx # Legacy pre-consolidation doc (for reference)
├── DEPENDENCIES.md # Module interaction map
└── TELEMETRY_SCHEMA.md # CSV schema definition for telemetry output



---

## Development Standards
- All code must compile in NinjaTrader 8 without warnings or duplicate definitions.
- All `[NinjaScriptProperty]` inputs must be explicitly defaulted in `OnStateChange (State.SetDefaults)`.
- Helper methods (`Clamp01`, `Squash`, `Blend`) live **only** in `MNQRSTest_Utilities.cs`.
- Multi-timeframe code is guarded by `UseMTFValidation` and `CurrentBars[..]` checks.
- Logging must go through a single telemetry dispatcher with headers matching `/docs/TELEMETRY_SCHEMA.md`.

See `/docs/CODER_HANDBOOK.md` for full rules.

---

## Historical Notes
- The `Module Info For Parity.docx` is retained for context. It describes module boundaries and design rationale from the pre-consolidation era. **Use only for reference; current code structure is in this repo’s `/Strategies/MNQRSTest/` folder.**
- Telemetry and CSV schema are aligned with Python replica engine to ensure one-to-one metric parity.

---

## Next Steps
1. **Compile in NinjaTrader 8** → confirm no syntax errors.
2. **Export Error Grid (CSV)** if compile fails → commit under `/error-grids/` for tracking.
3. **Agent cleanup** → feed CSV back into the agent loop to patch brace/namespace errors, unify fields, and simplify telemetry.
