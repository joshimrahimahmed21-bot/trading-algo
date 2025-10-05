MNQRSTest – Adaptive Quantitative Strategy (NinjaTrader 8)
📌 Overview

MNQRSTest is an advanced, modular NinjaTrader 8 strategy designed for adaptive breakout and momentum trading on intraday futures instruments.
The system evolves dynamically through multiple “passes,” each building on the prior architecture to improve robustness, interpretability, and machine-learning readiness.

The strategy currently trades using:

Multi-contract (core + runner) structure for flexible trade management

Composite quality scoring across momentum, positional volume, trend strength, and market structure

Fully configurable filters (volatility, trend slope, session time, space/resistance, quality gate)

Regime-aware thresholds and optional auto-switching logic (Pass 27 complete)

Comprehensive logging and dataset generation for post-analysis and ML research

🧭 Current State (Post-Pass 27)

As of October 2025, MNQRSTest is stable and research-ready:

✅ Multi-contract sizing (BaseContracts) and runner exit logic fully validated

✅ Regime-aware filters (ATR, Trend, Quality, Space) operational

✅ Full trade, setup, and equity logging for analytics and ML input

⏸️ Development paused for research and node architecture planning

The next phase focuses on wiring Relativity (temporal normalization and decay nodes) and modularizing Momentum Core and Positional Volume so that each can act as a pluggable signal node with its own recency weighting and context awareness.

🧮 Relativity Concept

Relativity is a universal normalization layer for signals.
Instead of treating each metric (ATR, PosVol, Momentum) on an absolute scale, Relativity maps it to its own recent distribution using rolling statistics or EWMA decay:

Relativity
(
𝑥
𝑡
)
=
𝑥
𝑡
−
𝜇
𝑡
𝜎
𝑡
,
𝜇
𝑡
=
(
1
−
𝛼
)
𝜇
𝑡
−
1
+
𝛼
𝑥
𝑡
Relativity(x
t
	​

)=
σ
t
	​

x
t
	​

−μ
t
	​

	​

,μ
t
	​

=(1−α)μ
t−1
	​

+αx
t
	​


This allows signals to be compared and weighted consistently regardless of market volatility regime.
The same architecture will later support temporal decay (“recent bars matter more”) and adaptive node weighting.

⚙️ Next Development Phase
Pause Node: Relativity & MomentumPosVol Assessment

Wire Relativity as a portable signal transform function (not a monolithic module).

Refactor Momentum Core and Positional Volume into independent nodes with Relativity and Decay capabilities.

Validate filters with sane defaults (e.g. ATR filter active with realistic bounds).

Prepare for mass optimizer sweeps and data generation for regime detection research.

Pass 28 – Data-Driven Regime Detection

Implement true regime logic using Relativized metrics (e.g. normalized ATR + ADX + PosVol structure).
Include hysteresis to avoid flip-flopping and record regime transitions in logs.

Pass 29 – Regime-Specific Trade Management

Allow regimes to modify runner behavior (TP length, timeout, allocation) and filter weights.

Pass 30 – Optimization & ML Sweeps

Run large-scale optimizer and Monte Carlo equity sims to derive statistically robust parameter sets.
Generate datasets for regime classifier training.

Pass 31 – Deployment & Risk Hardening

Finalize dynamic risk module, add kill-switches and equity curve guardrails, and document for live trading and prop account deployment.

🔍 Research Goals During Pause

Quantify how PosVol and Momentum correlate with subsequent breakout continuation.

Determine optimal Relativity/Decay parameters for signal stability vs responsiveness.

Identify volatility and volume patterns that differentiate Trend vs Chop regimes.

Back-test various runner management profiles to map regime → TP length relationships.

Run optimizer sweeps to rebuild data-driven regime switch logic based on proof data.

🧰 Running and Testing

Compile Strategy in NinjaTrader 8 (MNQRSTest in Strategy folder).

Analyzer Run: Use Strategy Analyzer → select instrument (e.g. MNQ 1-min).

Parameters of Interest: BaseContracts, ApplyRunnerManagement, UseQualityGate, MinQTotal2, UseRegimes.

Logs: Output to Documents\\NinjaTrader 8\\log → RunInfo.txt, Trades.csv, CompletedTrades.csv, Setups.csv, EquityCurve.csv.

Optimization: Set LogSetupsVerbose = false for speed; enable Genetic Optimizer for large parameter spaces.
