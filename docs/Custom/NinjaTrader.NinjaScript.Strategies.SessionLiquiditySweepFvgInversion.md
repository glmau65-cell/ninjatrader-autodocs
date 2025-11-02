# SessionLiquiditySweepFvgInversion

Base class: `NinjaTrader.NinjaScript.Strategies.Strategy`

## Description
Session Liquidity Sweep FVG Inversion Strategy - Trades session liquidity sweeps with Fair Value Gap inversions. This strategy tracks three trading sessions (Asian, London, and NY PM), identifies liquidity sweeps when price breaches session highs/lows, detects Fair Value Gaps (FVG), waits for FVG inversions, and enters trades accordingly.

## Session Time Properties
- `AsianStartHour` (int) - Asian session start hour (24hr format, EST)
- `AsianStartMinute` (int) - Asian session start minute
- `AsianEndHour` (int) - Asian session end hour (24hr format, EST)
- `AsianEndMinute` (int) - Asian session end minute
- `LondonStartHour` (int) - London session start hour (24hr format, EST)
- `LondonStartMinute` (int) - London session start minute
- `LondonEndHour` (int) - London session end hour (24hr format, EST)
- `LondonEndMinute` (int) - London session end minute
- `NYPMStartHour` (int) - NY PM session start hour (24hr format, EST)
- `NYPMStartMinute` (int) - NY PM session start minute
- `NYPMEndHour` (int) - NY PM session end hour (24hr format, EST)
- `NYPMEndMinute` (int) - NY PM session end minute

## FVG Detection Properties
- `FVGTimeframeMinutes` (int) - Timeframe for FVG detection (0 = chart timeframe)
- `MinFVGSizeTicks` (int) - Minimum FVG size in ticks to be valid
- `FVGValidityBars` (int) - How many bars to monitor FVG for inversion (0 = unlimited)

## Entry Settings Properties
- `EnableAsianTrades` (bool) - Enable trading on Asian session sweeps
- `EnableLondonTrades` (bool) - Enable trading on London session sweeps
- `EnableNYPMTrades` (bool) - Enable trading on NY PM session sweeps
- `MaxTradesPerSession` (int) - Maximum trades allowed per session (0 = unlimited)
- `SweepBufferTicks` (int) - Minimum ticks beyond high/low for valid sweep

## Risk Management Properties
- `StopLossType` (StopLossMode) - Stop loss calculation mode (Ticks or ATR)
- `StopLossTicks` (int) - Stop loss in ticks (if mode = Ticks)
- `StopLossATRMultiplier` (double) - ATR multiplier for stop loss (if mode = ATR)
- `TakeProfitType` (TakeProfitMode) - Take profit calculation mode (Ticks, ATR, or RiskReward)
- `TakeProfitTicks` (int) - Take profit in ticks (if mode = Ticks)
- `TakeProfitATRMultiplier` (double) - ATR multiplier for take profit (if mode = ATR)
- `RiskRewardRatio` (double) - Risk:Reward ratio (if mode = RiskReward)
- `ATRPeriod` (int) - ATR period for calculations
- `PositionSize` (int) - Number of contracts per trade
- `MaxDailyLoss` (double) - Maximum daily loss in currency (0 = no limit)
- `MaxDailyProfit` (double) - Maximum daily profit target in currency (0 = no limit)
- `EnableTrailingStop` (bool) - Enable trailing stop functionality

## Visual Settings Properties
- `DrawSessionLines` (bool) - Draw session high/low lines on chart
- `DrawFVGZones` (bool) - Draw FVG zones on chart
- `DrawEntryArrows` (bool) - Draw entry arrows on chart
- `DrawSessionBackgrounds` (bool) - Draw session background colors

## Strategy Logic

### Entry Rules - HIGH Breach (Liquidity Sweep Above)
1. Detect when price breaches a session high (Asian/London/NYPM)
2. Identify bullish Fair Value Gap (FVG) after the breach
   - Bullish FVG: gap between bar[2].Low and bar[0].High where bar[0].Low > bar[2].High
3. Wait for candle close BELOW the bullish FVG (inversion)
4. Enter SHORT position

### Entry Rules - LOW Breach (Liquidity Sweep Below)
1. Detect when price breaches a session low (Asian/London/NYPM)
2. Identify bearish Fair Value Gap (FVG) after the breach
   - Bearish FVG: gap between bar[2].High and bar[0].Low where bar[0].High < bar[2].Low
3. Wait for candle close ABOVE the bearish FVG (inversion)
4. Enter LONG position

### Exit Management
- Stop Loss: Configurable ticks or ATR-based
- Take Profit: Configurable ticks, ATR-based, or risk-reward ratio
- Optional trailing stop functionality

## Inner Enums
- `SessionType` - Asian, London, NYPM
- `StopLossMode` - Ticks, ATR
- `TakeProfitMode` - Ticks, ATR, RiskReward

## Inner Classes
- `SessionData` - Tracks individual session information (high, low, swept status, trade count)
- `FairValueGap` - Represents a detected FVG with its properties (bullish/bearish, gap levels, inversion status)
