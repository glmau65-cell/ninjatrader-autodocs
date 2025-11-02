# SessionLiquiditySweepFvgInversion

Base class: `NinjaTrader.NinjaScript.Strategies.Strategy`

## Summary
Session Liquidity Sweep FVG Inversion strategy that tracks session highs/lows, detects liquidity sweeps, finds Fair Value Gaps, and enters trades on FVG inversions.

## Properties
- `SessionStartHour` (int) – Session start hour (24h)
- `SessionEndHour` (int) – Session end hour (24h)
- `MinFVGSizeTicks` (int) – Minimum FVG size (ticks)
- `StopLossTicks` (int) – Stop loss size (ticks)
- `TakeProfitTicks` (int) – Take profit size (ticks)
- `Quantity` (int) – Order quantity

## Logic Overview
1. Track session high/low between configured start and end hours.
2. Mark a liquidity sweep when price breaks session high or low.
3. After a sweep, detect Fair Value Gaps (FVG) using the last three bars.
4. Store recent FVGs and wait for a close back inside the gap to confirm inversion.
5. Enter short after bullish FVG inversion or long after bearish FVG inversion.
6. Apply configurable stop loss and take profit.
