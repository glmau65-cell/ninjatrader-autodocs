using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Strategies
{
    [Serializable]
    [DataContract]
    public class CounterTrendBigBarStrategy : Strategy
    {
        private EMA ema;
        private ADX adx;
        private ATR atr;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Counter-trend strategy that trades reversals when big bars appear in ranging conditions on 15-minute charts.";
                Name = "CounterTrendBigBarStrategy";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = false;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 50;
                IsInstantiatedOnEachOptimizationIteration = true;

                AtrPeriod = 14;
                BigBarAtrMultiplier = 1.5;
                EmaPeriod = 50;
                AdxPeriod = 14;
                AdxRangeThreshold = 25;
                StopLossAtrMultiplier = 1.5;
                ProfitTargetAtrMultiplier = 2.5;
            }
            else if (State == State.Configure)
            {
                // No additional configuration required
            }
            else if (State == State.DataLoaded)
            {
                ema = EMA(EmaPeriod);
                adx = ADX(AdxPeriod);
                atr = ATR(AtrPeriod);

                if (ema != null)
                    AddChartIndicator(ema);
                if (adx != null)
                    AddChartIndicator(adx);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade)
                return;

            if (ema == null || adx == null || atr == null)
                return;

            if (TickSize.ApproxCompare(0) <= 0)
                return;

            double emaValue = ema[0];
            double adxValue = adx[0];
            double atrValue = atr[0];

            if (double.IsNaN(emaValue) || double.IsNaN(adxValue) || double.IsNaN(atrValue))
                return;

            if (atrValue.ApproxCompare(0) <= 0)
                return;

            double currentBarSize = High[0] - Low[0];
            double bigBarThreshold = BigBarAtrMultiplier * atrValue;
            bool isBigBar = currentBarSize >= bigBarThreshold;
            bool isRanging = adxValue < AdxRangeThreshold;

            if (Position.MarketPosition != MarketPosition.Flat)
                return;

            bool isGreenBar = Close[0] > Open[0];
            bool isRedBar = Close[0] < Open[0];
            bool priceAboveEma = Close[0] > emaValue;
            bool priceBelowEma = Close[0] < emaValue;

            if (priceBelowEma && isRanging && isGreenBar && isBigBar)
            {
                double stopPrice = Close[0] - (StopLossAtrMultiplier * atrValue);
                double targetPrice = Close[0] + (ProfitTargetAtrMultiplier * atrValue);

                SetStopLoss("LongEntry", CalculationMode.Price, stopPrice, false);
                SetProfitTarget("LongEntry", CalculationMode.Price, targetPrice);
                EnterLong("LongEntry");
            }
            else if (priceAboveEma && isRanging && isRedBar && isBigBar)
            {
                double stopPrice = Close[0] + (StopLossAtrMultiplier * atrValue);
                double targetPrice = Close[0] - (ProfitTargetAtrMultiplier * atrValue);

                SetStopLoss("ShortEntry", CalculationMode.Price, stopPrice, false);
                SetProfitTarget("ShortEntry", CalculationMode.Price, targetPrice);
                EnterShort("ShortEntry");
            }
        }

        #region Properties
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ATR Period", Description = "Period for ATR indicator.", Order = 1, GroupName = "Parameters")]
        [DataMember]
        public int AtrPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, double.MaxValue)]
        [Display(Name = "Big Bar ATR Multiplier", Description = "Multiplier to determine big bar threshold (Bar Size >= Multiplier × ATR).", Order = 2, GroupName = "Parameters")]
        [DataMember]
        public double BigBarAtrMultiplier { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "EMA Period", Description = "Period for EMA indicator.", Order = 3, GroupName = "Parameters")]
        [DataMember]
        public int EmaPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ADX Period", Description = "Period for ADX indicator.", Order = 4, GroupName = "Parameters")]
        [DataMember]
        public int AdxPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "ADX Range Threshold", Description = "ADX threshold below which market is considered ranging.", Order = 5, GroupName = "Parameters")]
        [DataMember]
        public double AdxRangeThreshold { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, double.MaxValue)]
        [Display(Name = "Stop Loss ATR Multiplier", Description = "Multiplier for stop loss distance (Stop Loss = Multiplier × ATR).", Order = 6, GroupName = "Parameters")]
        [DataMember]
        public double StopLossAtrMultiplier { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, double.MaxValue)]
        [Display(Name = "Profit Target ATR Multiplier", Description = "Multiplier for profit target distance (Profit Target = Multiplier × ATR).", Order = 7, GroupName = "Parameters")]
        [DataMember]
        public double ProfitTargetAtrMultiplier { get; set; }
        #endregion
    }
}
