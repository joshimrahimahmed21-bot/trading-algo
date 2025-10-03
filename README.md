# MNQRSTest Trading Algorithm

A NinjaTrader 8 strategy (C#) for micro NASDAQ (MNQ) futures, developed iteratively in **passes**.  
This repo combines the **source code** and a structured **development log** to ensure continuity across sessions and contributors.

---

## 📂 Repo Structure

- **/Strategy_files**  
  All NinjaTrader 8 strategy partials (`Config.cs`, `EntryQuality.cs`, `SizingRunner.cs`, `LoggingHardLock.cs`, etc.).

- **/docs**  
  Contains the *Golden Context* documentation, split into four main sections:  
  - **Pass Roadmap** – Iterative development steps (1–20+).  
  - **Coder Handbook & Best Practices** – Conventions, logging, debug, sizing rules.  
  - **Current System State Summary** – What’s working now, what’s in progress.  
  - **NinjaTrader Quirks & Discoveries** – Platform-specific gotchas and lessons learned.

---

## 🔄 Development Workflow

- Work is organized into **passes**. Each pass delivers one self-contained feature, fix, or refinement.  
- After each pass:  
  1. Code is updated in `/Strategy_files`.  
  2. Logs/docs in `/docs` are updated (especially the **Current System State** and **Roadmap**).  
  3. Commit/branch is tagged with the pass number (e.g. `pass-16.5-basecontracts`).  

This ensures future sessions or agents can pick up exactly where the last left off.

---

## 📌 Current Status

- **Pass 16** complete: introduced `BaseContracts` param to replace NinjaTrader’s unreliable `DefaultQuantity`.  
- **Pass 16.5 (in progress)**: wire `BaseContracts` into all `EnterLong/EnterShort` calls, confirm CORE/RUNNER split sizing, and update Trades.csv with a `Contracts` column.  
- Next pass: **Pass 17** – risk sizing modes (currency-based, account-fraction).

---

## ⚠️ Important NinjaTrader Notes

- Always keep `DefaultQuantity = 1` (fixed in `SetDefaults`).  
- Position sizing is controlled entirely by **BaseContracts**.  
- All order methods must use explicit overloads (`EnterLong(qty, "tag")`).  
- Logs:  
  - `Trades.csv` – entry records (CORE, RUNNER, Single).  
  - `Exits.csv` – exit fills (Target, Stop, Breakeven).  
  - `Setups.csv` – signals, skips, and reasons.  
  - `RunInfo.txt` – snapshot of all param settings.  

See **/docs** for the detailed handbook and roadmap.

---

## 🚀 Getting Started

1. Open NinjaTrader 8.  
2. Add the strategy files from `/Strategy_files` to `Documents/NinjaTrader 8/bin/Custom/Strategies`.  
3. Recompile and enable `MNQRSTest`.  
4. Review `/docs/Current System State Summary.md` for the latest instructions on parameter defaults and known caveats.  

---

## 🤝 Contributing

- Follow the **pass workflow**: one feature/fix per pass.  
- Update both code and docs.  
- Tag commits/branches with pass numbers.  
- Use `ForceEntry` (debug param) only for smoke tests.  

---

This repo is designed so that **any new agent or contributor can pick up exactly where the last left off**, without rediscovering old bugs or quirks.  
