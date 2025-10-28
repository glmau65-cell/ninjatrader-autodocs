using System;
using System.Collections.Generic;
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
    public class AdvancedScalpingStrategy : Strategy
    {
        private enum TradeDirection
        {
            None,
            Long,
            Short
        }

        private EMA fastEma;
        private EMA slowEma;
        private RSI rsi;
        private SMA volumeSma;

        private double lastFiveMinuteHigh;
        private double lastFiveMinuteLow;

        private bool longBreakoutTriggered;
        private bool shortBreakoutTriggered;
        private double longBreakoutLevel;
        private double shortBreakoutLevel;
        private int longBreakoutBar;
        private int shortBreakoutBar;

        private readonly Dictionary<string, double> entryPrices = new Dictionary<string, double>();
        private readonly HashSet<string> activeEntrySignals = new HashSet<string>();

        private bool longFirstTargetHit;
        private bool shortFirstTargetHit;

        private TradeDirection activeTradeDirection = TradeDirection.None;
        private double activeTradeRealizedPnl;

        private int tradesThisSession;
        private int losingTradesThisSession;
        private DateTime sessionDate;
        private int lastProcessedTradeCount;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Advanced scalping strategy leveraging London/NY overlap, breakout/retest logic, EMA alignment, RSI filter, volume confirmation, and disciplined risk management.";
                Name = "AdvancedScalpingStrategy";
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
                BarsRequiredToTrade = 100;
                IsInstantiatedOnEachOptimizationIteration = true;

                AccountSize = 50000;
                RiskPercent = 0.005;
                PipSize = 0.0001;
                PipValuePerLot = 10;
                StopLossPips = 6;
                TakeProfitPips = 12;
                TrailingStopPips = 5;
                StructureBufferPips = 1;
                RetestBars = 3;
                VolumeSmaPeriod = 20;
                VolumeMultiplier = 1.2;
                MinAverageVolume = 100000;
                SessionStartTime = 80000;
                SessionEndTime = 110000;
                FastEmaPeriod = 20;
                SlowEmaPeriod = 50;
                RsiPeriod = 14;
                RsiSmoothing = 3;
                RsiLowerBand = 40;
                RsiUpperBand = 60;
            }
            else if (State == State.Configure)
            {
                AddDataSeries(BarsPeriodType.Minute, 5);
            }
            else if (State == State.DataLoaded)
            {
                fastEma = EMA(BarsArray[0], FastEmaPeriod);
                slowEma = EMA(BarsArray[0], SlowEmaPeriod);
                rsi = RSI(BarsArray[0], RsiPeriod, RsiSmoothing);
                volumeSma = SMA(Volumes[0], VolumeSmaPeriod);

                AddChartIndicator(fastEma);
                AddChartIndicator(slowEma);
                AddChartIndicator(rsi);
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress == 1)
            {
                if (CurrentBars[1] < 1)
                    return;

                lastFiveMinuteHigh = Highs[1][0];
                lastFiveMinuteLow = Lows[1][0];
                return;
            }

            if (BarsInProgress != 0)
                return;

            if (CurrentBar < BarsRequiredToTrade)
                return;

            if (!IsWithinTradingWindow(Time[0]))
            {
                ResetSessionIfNeeded(Time[0]);
                ResetBreakoutFlags();
                return;
            }

            ResetSessionIfNeeded(Time[0]);

            if (!IsVolumeQualified())
                return;

            if (!IsPriceAligned())
            {
                ResetBreakoutFlags();
                return;
            }

            if (!IsRsiQualified())
                return;

            ProcessBreakoutLogic();
            ManageOpenTrades();
        }

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            base.OnExecutionUpdate(execution, executionId, price, quantity, marketPosition, orderId, time);

            if (execution == null || execution.Order == null)
                return;

            if (execution.Order.OrderState != OrderState.Filled && execution.Order.OrderState != OrderState.PartFilled)
                return;

            string entrySignal = execution.Order.FromEntrySignal;

            if (!string.IsNullOrEmpty(entrySignal) && (entrySignal.StartsWith("LongEntry") || entrySignal.StartsWith("ShortEntry")))
            {
                if (execution.Order.OrderAction == OrderAction.Buy || execution.Order.OrderAction == OrderAction.SellShort)
                {
                    entryPrices[entrySignal] = execution.Price;
                    activeEntrySignals.Add(entrySignal);

                    if (activeTradeDirection == TradeDirection.None)
                    {
                        activeTradeDirection = entrySignal.StartsWith("Long") ? TradeDirection.Long : TradeDirection.Short;
                        activeTradeRealizedPnl = 0;
                        tradesThisSession++;
                    }
                }
                else if (execution.Order.OrderAction == OrderAction.Sell || execution.Order.OrderAction == OrderAction.BuyToCover)
                {
                    activeTradeRealizedPnl += execution.ProfitCurrency;
                    activeEntrySignals.Remove(entrySignal);

                    if (entrySignal == "LongEntryHalf1" && !longFirstTargetHit && execution.Order.OrderAction == OrderAction.Sell)
                    {
                        longFirstTargetHit = true;
                        if (entryPrices.TryGetValue("LongEntryHalf2", out double remainingEntryPrice))
                        {
                            SetStopLoss("LongEntryHalf2", CalculationMode.Price, remainingEntryPrice, false);
                            int trailingTicks = PipsToTicks(TrailingStopPips);
                            if (trailingTicks > 0)
                                SetTrailStop("LongEntryHalf2", CalculationMode.Ticks, trailingTicks, false);
                        }
                    }

                    if (entrySignal == "ShortEntryHalf1" && !shortFirstTargetHit && execution.Order.OrderAction == OrderAction.BuyToCover)
                    {
                        shortFirstTargetHit = true;
                        if (entryPrices.TryGetValue("ShortEntryHalf2", out double remainingEntryPrice))
                        {
                            SetStopLoss("ShortEntryHalf2", CalculationMode.Price, remainingEntryPrice, false);
                            int trailingTicks = PipsToTicks(TrailingStopPips);
                            if (trailingTicks > 0)
                                SetTrailStop("ShortEntryHalf2", CalculationMode.Ticks, trailingTicks, false);
                        }
                    }

                    if (activeEntrySignals.Count == 0 && activeTradeDirection != TradeDirection.None)
                    {
                        if (activeTradeRealizedPnl < 0)
                            losingTradesThisSession++;

                        activeTradeDirection = TradeDirection.None;
                        activeTradeRealizedPnl = 0;
                        longFirstTargetHit = false;
                        shortFirstTargetHit = false;
                        entryPrices.Clear();
                        ResetBreakoutFlags();
                    }
                }
            }
        }

        private void ProcessBreakoutLogic()
        {
            if (double.IsNaN(lastFiveMinuteHigh) || double.IsNaN(lastFiveMinuteLow))
                return;

            int effectiveMaxTrades = losingTradesThisSession >= 2 && tradesThisSession >= 2 ? 3 : int.MaxValue;
            if (tradesThisSession >= effectiveMaxTrades)
                return;

            int stopTicks = PipsToTicks(StopLossPips);
            int firstTargetTicks = stopTicks;
            int secondTargetTicks = PipsToTicks(TakeProfitPips);

            if (stopTicks <= 0 || secondTargetTicks <= 0)
                return;

            double breakoutBuffer = PipSize * 0.5;

            if (!longBreakoutTriggered)
            {
                if (High[0] > lastFiveMinuteHigh + breakoutBuffer && VolumeIsConfirming())
                {
                    longBreakoutTriggered = true;
                    longBreakoutLevel = lastFiveMinuteHigh;
                    longBreakoutBar = CurrentBar;
                }
            }
            else
            {
                if (CurrentBar - longBreakoutBar > RetestBars)
                {
                    longBreakoutTriggered = false;
                }
                else if (Low[0] <= longBreakoutLevel + breakoutBuffer && Close[0] > longBreakoutLevel && CanEnterLongTrade())
                {
                    SubmitLongEntries(stopTicks, firstTargetTicks, secondTargetTicks);
                    longBreakoutTriggered = false;
                }
            }

            if (!shortBreakoutTriggered)
            {
                if (Low[0] < lastFiveMinuteLow - breakoutBuffer && VolumeIsConfirming())
                {
                    shortBreakoutTriggered = true;
                    shortBreakoutLevel = lastFiveMinuteLow;
                    shortBreakoutBar = CurrentBar;
                }
            }
            else
            {
                if (CurrentBar - shortBreakoutBar > RetestBars)
                {
                    shortBreakoutTriggered = false;
                }
                else if (High[0] >= shortBreakoutLevel - breakoutBuffer && Close[0] < shortBreakoutLevel && CanEnterShortTrade())
                {
                    SubmitShortEntries(stopTicks, firstTargetTicks, secondTargetTicks);
                    shortBreakoutTriggered = false;
                }
            }
        }

        private void SubmitLongEntries(int stopTicks, int firstTargetTicks, int secondTargetTicks)
        {
            int quantity = CalculatePositionSize();
            if (quantity <= 0)
                return;

            int halfQuantity = Math.Max(1, quantity / 2);
            int remainderQuantity = quantity - halfQuantity;
            if (remainderQuantity <= 0)
                remainderQuantity = halfQuantity;

            SetStopLoss("LongEntryHalf1", CalculationMode.Ticks, stopTicks, false);
            SetProfitTarget("LongEntryHalf1", CalculationMode.Ticks, firstTargetTicks);

            SetStopLoss("LongEntryHalf2", CalculationMode.Ticks, stopTicks, false);
            SetProfitTarget("LongEntryHalf2", CalculationMode.Ticks, secondTargetTicks);

            EnterLong(halfQuantity, "LongEntryHalf1");
            EnterLong(remainderQuantity, "LongEntryHalf2");
        }

        private void SubmitShortEntries(int stopTicks, int firstTargetTicks, int secondTargetTicks)
        {
            int quantity = CalculatePositionSize();
            if (quantity <= 0)
                return;

            int halfQuantity = Math.Max(1, quantity / 2);
            int remainderQuantity = quantity - halfQuantity;
            if (remainderQuantity <= 0)
                remainderQuantity = halfQuantity;

            SetStopLoss("ShortEntryHalf1", CalculationMode.Ticks, stopTicks, false);
            SetProfitTarget("ShortEntryHalf1", CalculationMode.Ticks, firstTargetTicks);

            SetStopLoss("ShortEntryHalf2", CalculationMode.Ticks, stopTicks, false);
            SetProfitTarget("ShortEntryHalf2", CalculationMode.Ticks, secondTargetTicks);

            EnterShort(halfQuantity, "ShortEntryHalf1");
            EnterShort(remainderQuantity, "ShortEntryHalf2");
        }

        private void ManageOpenTrades()
        {
            double structureBufferPrice = StructureBufferPips * PipSize;

            if (Position.MarketPosition == MarketPosition.Long && longBreakoutLevel > 0)
            {
                double exitPrice = longBreakoutLevel - structureBufferPrice;
                if (Close[0] <= exitPrice)
                {
                    ExitLong("StructureBreakLong1", "LongEntryHalf1");
                    ExitLong("StructureBreakLong2", "LongEntryHalf2");
                }
            }
            else if (Position.MarketPosition == MarketPosition.Short && shortBreakoutLevel > 0)
            {
                double exitPrice = shortBreakoutLevel + structureBufferPrice;
                if (Close[0] >= exitPrice)
                {
                    ExitShort("StructureBreakShort1", "ShortEntryHalf1");
                    ExitShort("StructureBreakShort2", "ShortEntryHalf2");
                }
            }
        }

        private bool IsWithinTradingWindow(DateTime time)
        {
            int current = ToTime(time);
            return current >= SessionStartTime && current <= SessionEndTime;
        }

        private void ResetSessionIfNeeded(DateTime time)
        {
            if (sessionDate != time.Date)
            {
                sessionDate = time.Date;
                tradesThisSession = 0;
                losingTradesThisSession = 0;
            }
        }

        private int CalculatePositionSize()
        {
            double riskCapital = AccountSize * RiskPercent;
            double riskPerLot = StopLossPips * PipValuePerLot;

            if (riskPerLot <= 0)
                return 0;

            int quantity = (int)Math.Floor(riskCapital / riskPerLot);
            return Math.Max(1, quantity);
        }

        private bool CanEnterLongTrade()
        {
            if (activeTradeDirection != TradeDirection.None)
                return false;

            return fastEma[0] > slowEma[0];
        }

        private bool CanEnterShortTrade()
        {
            if (activeTradeDirection != TradeDirection.None)
                return false;

            return fastEma[0] < slowEma[0];
        }

        private bool IsVolumeQualified()
        {
            if (volumeSma == null || volumeSma[0] <= 0)
                return false;

            if (volumeSma[0] < MinAverageVolume)
                return false;

            return Volumes[0][0] >= volumeSma[0] * VolumeMultiplier;
        }

        private bool VolumeIsConfirming()
        {
            if (volumeSma == null || volumeSma[0] <= 0)
                return false;

            return Volumes[0][0] >= volumeSma[0] * VolumeMultiplier;
        }

        private bool IsPriceAligned()
        {
            return Math.Abs(fastEma[0] - slowEma[0]) >= PipSize;
        }

        private bool IsRsiQualified()
        {
            if (rsi == null)
                return false;

            double rsiValue = rsi[0];
            return rsiValue >= RsiLowerBand && rsiValue <= RsiUpperBand;
        }

        private void ResetBreakoutFlags()
        {
            longBreakoutTriggered = false;
            shortBreakoutTriggered = false;
            longBreakoutLevel = 0;
            shortBreakoutLevel = 0;
            longBreakoutBar = 0;
            shortBreakoutBar = 0;
        }

        private int PipsToTicks(double pips)
        {
            if (TickSize <= 0)
                return 0;

            double priceDistance = pips * PipSize;
            return Math.Max(1, (int)Math.Round(priceDistance / TickSize));
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Fast EMA Period", Order = 1, GroupName = "Trend Filters")]
        [DataMember]
        public int FastEmaPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Slow EMA Period", Order = 2, GroupName = "Trend Filters")]
        [DataMember]
        public int SlowEmaPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "RSI Period", Order = 3, GroupName = "Oscillator")]
        [DataMember]
        public int RsiPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "RSI Smoothing", Order = 4, GroupName = "Oscillator")]
        [DataMember]
        public int RsiSmoothing { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 100.0)]
        [Display(Name = "RSI Lower Band", Order = 5, GroupName = "Oscillator")]
        [DataMember]
        public double RsiLowerBand { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 100.0)]
        [Display(Name = "RSI Upper Band", Order = 6, GroupName = "Oscillator")]
        [DataMember]
        public double RsiUpperBand { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Retest Bars", Order = 1, GroupName = "Breakout")]
        [DataMember]
        public int RetestBars { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, double.MaxValue)]
        [Display(Name = "Structure Buffer (pips)", Order = 2, GroupName = "Breakout")]
        [DataMember]
        public double StructureBufferPips { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Volume SMA Period", Order = 1, GroupName = "Volume")]
        [DataMember]
        public int VolumeSmaPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, double.MaxValue)]
        [Display(Name = "Volume Multiplier", Order = 2, GroupName = "Volume")]
        [DataMember]
        public double VolumeMultiplier { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, double.MaxValue)]
        [Display(Name = "Minimum Average Volume", Order = 3, GroupName = "Volume")]
        [DataMember]
        public double MinAverageVolume { get; set; }

        [NinjaScriptProperty]
        [Range(0.00001, double.MaxValue)]
        [Display(Name = "Pip Size", Order = 1, GroupName = "Risk")]
        [DataMember]
        public double PipSize { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, double.MaxValue)]
        [Display(Name = "Pip Value Per Lot", Order = 2, GroupName = "Risk")]
        [DataMember]
        public double PipValuePerLot { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, double.MaxValue)]
        [Display(Name = "Account Size", Order = 3, GroupName = "Risk")]
        [DataMember]
        public double AccountSize { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 0.02)]
        [Display(Name = "Risk Percent", Order = 4, GroupName = "Risk")]
        [DataMember]
        public double RiskPercent { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, double.MaxValue)]
        [Display(Name = "Stop Loss (pips)", Order = 5, GroupName = "Risk")]
        [DataMember]
        public double StopLossPips { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, double.MaxValue)]
        [Display(Name = "Take Profit (pips)", Order = 6, GroupName = "Risk")]
        [DataMember]
        public double TakeProfitPips { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, double.MaxValue)]
        [Display(Name = "Trailing Stop (pips)", Order = 7, GroupName = "Risk")]
        [DataMember]
        public double TrailingStopPips { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Session Start (HHmmss)", Order = 1, GroupName = "Session")]
        [DataMember]
        public int SessionStartTime { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Session End (HHmmss)", Order = 2, GroupName = "Session")]
        [DataMember]
        public int SessionEndTime { get; set; }

        #endregion
    }
}
