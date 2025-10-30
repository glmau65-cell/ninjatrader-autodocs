# SevenPM Breakout Strategy for NinjaTrader 8

## Overview
This NinjaTrader 8 strategy trades breakouts based on reference levels captured at 7:00 PM (or any configured time). The strategy includes sophisticated trade management with a configurable maximum number of trades per day.

## Strategy Logic

### Reference Level Setup
- At the configured reference time (default: 7:00 PM), the strategy captures the high and low of the current bar
- These levels become the breakout thresholds for the trading session

### Entry Conditions
**Long Entry:**
- Price closes above the reference high
- Daily trade limit not reached
- Not currently stopped out

**Short Entry:**
- Price closes below the reference low
- Daily trade limit not reached
- Not currently stopped out

### Trade Management
- **Max Trades Per Day:** Configurable limit (1-10) prevents overtrading
- **Stopped Out Protection:** After a stop loss, no more trades for the day
- **Daily Reset:** Reference levels and counters reset at the start of each new day

## Parameters

### 1. Reference Setup
- **Reference Hour** (0-23): Hour to capture reference levels (default: 19 = 7 PM)
- **Reference Minute** (0-59): Minute to capture reference levels (default: 0)
- **Reset Daily** (bool): Whether to reset reference levels daily (default: true)
- **Max Trades Per Day** (1-10): Maximum number of trades allowed per day (default: 1)

### 2. Risk Management
- **Stop Loss (Ticks)**: Stop loss distance in ticks (default: 10)
- **Take Profit 1 (Ticks)**: First profit target in ticks (default: 15)
- **Take Profit 2 (Ticks)**: Second profit target in ticks (default: 25)

## Installation
1. Copy `SevenPMBreakoutStrategy.cs` to your NinjaTrader 8 custom strategies folder:
   - Typically: `Documents\NinjaTrader 8\bin\Custom\Strategies\`
2. Compile the strategy in NinjaTrader (Tools → Compile)
3. Apply to a chart or use in Strategy Analyzer

## Best Practices
- Test thoroughly in simulation before live trading
- Adjust reference time based on your market's key levels
- Consider market volatility when setting stop loss and profit targets
- Monitor the trade counter to ensure proper daily limit enforcement

## Version History
- **v1.0**: Initial implementation with max trades per day feature
