# Counter-trend Big Bar Strategy for NinjaTrader 8

## Overview
This NinjaTrader 8 strategy trades counter-trend reversals when big bars (defined by ATR) appear during ranging market conditions on 15-minute charts.

## Strategy Logic

### Long Entry Conditions (all must be true)
1. **Price is below 50-period EMA** - Indicates we're in a downtrend
2. **ADX(14) < 25** - Market is ranging (weak trend)
3. **Green bar** - Current bar closes above its open (Close > Open)
4. **Big bar** - Current bar size (High - Low) >= 1.5 × ATR(14)
5. **Enter at market** - Entry triggered on bar close

### Short Entry Conditions (all must be true)
1. **Price is above 50-period EMA** - Indicates we're in an uptrend
2. **ADX(14) < 25** - Market is ranging (weak trend)
3. **Red bar** - Current bar closes below its open (Close < Open)
4. **Big bar** - Current bar size (High - Low) >= 1.5 × ATR(14)
5. **Enter at market** - Entry triggered on bar close

### Exit Rules

**Stop Loss:**
- **Long:** Entry price - (1.5 × ATR(14))
- **Short:** Entry price + (1.5 × ATR(14))

**Profit Target:**
- **Long:** Entry price + (2.5 × ATR(14))
- **Short:** Entry price - (2.5 × ATR(14))

## Strategy Parameters

All parameters are user-configurable in the NinjaTrader Strategy Analyzer and when applied to charts:

| Parameter | Default | Description |
|-----------|---------|-------------|
| ATR Period | 14 | Period for the ATR (Average True Range) indicator |
| Big Bar ATR Multiplier | 1.5 | Multiplier to determine big bar threshold (Bar Size >= Multiplier × ATR) |
| EMA Period | 50 | Period for the EMA (Exponential Moving Average) indicator |
| ADX Period | 14 | Period for the ADX (Average Directional Index) indicator |
| ADX Range Threshold | 25 | ADX threshold below which market is considered ranging |
| Stop Loss ATR Multiplier | 1.5 | Multiplier for stop loss distance (Stop Loss = Multiplier × ATR) |
| Profit Target ATR Multiplier | 2.5 | Multiplier for profit target distance (Profit Target = Multiplier × ATR) |

## Implementation Details

- **Inherits from:** `NinjaTrader.NinjaScript.Strategies.Strategy`
- **Calculate mode:** `OnBarClose` - Calculates only when a bar closes (recommended for 15-minute bars)
- **Position management:** One position at a time (`EntriesPerDirection = 1`)
- **Bars required:** 50 bars minimum before trading begins
- **Stop/Target handling:** `PerEntryExecution` - Each entry gets its own stop loss and profit target
- **Chart indicators:** EMA and ADX are automatically added to the chart for visualization

## Installation

1. Copy `CounterTrendBigBarStrategy.cs` to your NinjaTrader 8 `NinjaScript\Strategies` folder
2. Open NinjaTrader 8
3. Go to Tools > NinjaScript Editor (F11)
4. Right-click on "Strategies" and select "Compile"
5. If successful, the strategy will appear in your strategy list

## Usage

### Backtesting
1. Open the Strategy Analyzer (Tools > Strategy Analyzer or F7)
2. Click "Add Strategy" and select "CounterTrendBigBarStrategy"
3. Configure the instrument and time frame (recommended: 15-minute bars)
4. Adjust parameters as needed
5. Click "Run" to backtest

### Live Trading / Chart Application
1. Open a chart with your desired instrument
2. Right-click the chart and select Strategies
3. Click "Add" and select "CounterTrendBigBarStrategy"
4. Set the time frame to 15 minutes (recommended)
5. Configure parameters
6. Enable the strategy

## Risk Warning

This strategy is provided for educational purposes. Always:
- Test thoroughly in a simulation environment before live trading
- Understand the risks involved with algorithmic trading
- Use appropriate position sizing and risk management
- Monitor the strategy's performance regularly

## Technical Implementation

### Indicators Used
- **EMA (Exponential Moving Average):** Trend filter
- **ADX (Average Directional Index):** Measures trend strength
- **ATR (Average True Range):** Volatility measurement for dynamic position sizing

### Key Features
- Proper null and NaN checks for indicator values
- Uses NinjaTrader's ApproxCompare for floating-point comparisons
- Comprehensive parameter validation with Range attributes
- User-friendly parameter descriptions in the Strategy Analyzer
- Error handling and state management following NT8 best practices

## Support

For questions or issues specific to NinjaTrader, please refer to:
- [NinjaTrader Support Center](https://ninjatrader.com/support)
- [NinjaTrader 8 Help Guide](https://ninjatrader.com/support/helpGuides/nt8/)
- [NinjaTrader Community Forum](https://ninjatrader.com/support/forum)

## License

This strategy is provided as-is without any warranty. Use at your own risk.
