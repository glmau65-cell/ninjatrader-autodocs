using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;

[Serializable]
[DataContract]
namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SessionLiquiditySweepFvgInversion : NinjaTrader.NinjaScript.Strategies.Strategy
    {
        #region Enums
        private enum SessionType
        {
            Asian,
            London,
            NYPM
        }

        private enum StopLossMode
        {
            Ticks,
            ATR
        }

        private enum TakeProfitMode
        {
            Ticks,
            ATR,
            RiskReward
        }
        #endregion

        #region Inner Classes
        private class SessionData
        {
            public SessionType Type { get; set; }
            public double High { get; set; }
            public double Low { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public bool IsComplete { get; set; }
            public bool HighSwept { get; set; }
            public bool LowSwept { get; set; }
            public int TradesCount { get; set; }
            public string Tag { get; set; }

            public SessionData(SessionType type, DateTime start, DateTime end)
            {
                Type = type;
                StartTime = start;
                EndTime = end;
                High = double.MinValue;
                Low = double.MaxValue;
                IsComplete = false;
                HighSwept = false;
                LowSwept = false;
                TradesCount = 0;
                Tag = type.ToString() + "_" + start.ToString("yyyyMMddHHmm");
            }
        }

        private class FairValueGap
        {
            public bool IsBullish { get; set; }
            public double GapTop { get; set; }
            public double GapBottom { get; set; }
            public int BarIndex { get; set; }
            public DateTime CreatedTime { get; set; }
            public bool IsInverted { get; set; }
            public SessionType AssociatedSession { get; set; }
            public bool IsHighSweep { get; set; }
            public string Tag { get; set; }

            public FairValueGap(bool isBullish, double top, double bottom, int barIndex, DateTime time, SessionType session, bool isHighSweep)
            {
                IsBullish = isBullish;
                GapTop = top;
                GapBottom = bottom;
                BarIndex = barIndex;
                CreatedTime = time;
                IsInverted = false;
                AssociatedSession = session;
                IsHighSweep = isHighSweep;
                Tag = "FVG_" + time.ToString("yyyyMMddHHmmss");
            }

            public double GetSize()
            {
                return Math.Abs(GapTop - GapBottom);
            }
        }
        #endregion

        #region Private Variables
        private SessionData currentAsianSession;
        private SessionData currentLondonSession;
        private SessionData currentNYPMSession;
        private SessionData previousAsianSession;
        private SessionData previousLondonSession;
        private SessionData previousNYPMSession;

        private List<FairValueGap> activeFVGs;
        private NinjaTrader.NinjaScript.Indicators.ATR atr;
        private int fvgBarsInProgress = 0;
        private double dailyProfitLoss = 0;
        private DateTime lastTradingDay = DateTime.MinValue;
        #endregion

        #region Session Time Properties
        [DataMember]
        [Display(Name = "Asian Start Hour", Description = "Asian session start hour (24hr format, EST)", Order = 100, GroupName = "1. Session Times")]
        public int AsianStartHour { get; set; }

        [DataMember]
        [Display(Name = "Asian Start Minute", Description = "Asian session start minute", Order = 101, GroupName = "1. Session Times")]
        public int AsianStartMinute { get; set; }

        [DataMember]
        [Display(Name = "Asian End Hour", Description = "Asian session end hour (24hr format, EST)", Order = 102, GroupName = "1. Session Times")]
        public int AsianEndHour { get; set; }

        [DataMember]
        [Display(Name = "Asian End Minute", Description = "Asian session end minute", Order = 103, GroupName = "1. Session Times")]
        public int AsianEndMinute { get; set; }

        [DataMember]
        [Display(Name = "London Start Hour", Description = "London session start hour (24hr format, EST)", Order = 104, GroupName = "1. Session Times")]
        public int LondonStartHour { get; set; }

        [DataMember]
        [Display(Name = "London Start Minute", Description = "London session start minute", Order = 105, GroupName = "1. Session Times")]
        public int LondonStartMinute { get; set; }

        [DataMember]
        [Display(Name = "London End Hour", Description = "London session end hour (24hr format, EST)", Order = 106, GroupName = "1. Session Times")]
        public int LondonEndHour { get; set; }

        [DataMember]
        [Display(Name = "London End Minute", Description = "London session end minute", Order = 107, GroupName = "1. Session Times")]
        public int LondonEndMinute { get; set; }

        [DataMember]
        [Display(Name = "NY PM Start Hour", Description = "NY PM session start hour (24hr format, EST)", Order = 108, GroupName = "1. Session Times")]
        public int NYPMStartHour { get; set; }

        [DataMember]
        [Display(Name = "NY PM Start Minute", Description = "NY PM session start minute", Order = 109, GroupName = "1. Session Times")]
        public int NYPMStartMinute { get; set; }

        [DataMember]
        [Display(Name = "NY PM End Hour", Description = "NY PM session end hour (24hr format, EST)", Order = 110, GroupName = "1. Session Times")]
        public int NYPMEndHour { get; set; }

        [DataMember]
        [Display(Name = "NY PM End Minute", Description = "NY PM session end minute", Order = 111, GroupName = "1. Session Times")]
        public int NYPMEndMinute { get; set; }
        #endregion

        #region FVG Detection Properties
        [DataMember]
        [Display(Name = "FVG Timeframe (Minutes)", Description = "Timeframe for FVG detection (0 = chart timeframe)", Order = 200, GroupName = "2. FVG Detection")]
        public int FVGTimeframeMinutes { get; set; }

        [DataMember]
        [Display(Name = "Min FVG Size (Ticks)", Description = "Minimum FVG size in ticks to be valid", Order = 201, GroupName = "2. FVG Detection")]
        public int MinFVGSizeTicks { get; set; }

        [DataMember]
        [Display(Name = "FVG Validity Bars", Description = "How many bars to monitor FVG for inversion (0 = unlimited)", Order = 202, GroupName = "2. FVG Detection")]
        public int FVGValidityBars { get; set; }
        #endregion

        #region Entry Settings Properties
        [DataMember]
        [Display(Name = "Enable Asian Trades", Description = "Enable trading on Asian session sweeps", Order = 300, GroupName = "3. Entry Settings")]
        public bool EnableAsianTrades { get; set; }

        [DataMember]
        [Display(Name = "Enable London Trades", Description = "Enable trading on London session sweeps", Order = 301, GroupName = "3. Entry Settings")]
        public bool EnableLondonTrades { get; set; }

        [DataMember]
        [Display(Name = "Enable NY PM Trades", Description = "Enable trading on NY PM session sweeps", Order = 302, GroupName = "3. Entry Settings")]
        public bool EnableNYPMTrades { get; set; }

        [DataMember]
        [Display(Name = "Max Trades Per Session", Description = "Maximum trades allowed per session (0 = unlimited)", Order = 303, GroupName = "3. Entry Settings")]
        public int MaxTradesPerSession { get; set; }

        [DataMember]
        [Display(Name = "Sweep Buffer (Ticks)", Description = "Minimum ticks beyond high/low for valid sweep", Order = 304, GroupName = "3. Entry Settings")]
        public int SweepBufferTicks { get; set; }
        #endregion

        #region Risk Management Properties
        [DataMember]
        [Display(Name = "Stop Loss Mode", Description = "Stop loss calculation mode", Order = 400, GroupName = "4. Risk Management")]
        public StopLossMode StopLossType { get; set; }

        [DataMember]
        [Display(Name = "Stop Loss Ticks", Description = "Stop loss in ticks (if mode = Ticks)", Order = 401, GroupName = "4. Risk Management")]
        public int StopLossTicks { get; set; }

        [DataMember]
        [Display(Name = "Stop Loss ATR Multiplier", Description = "ATR multiplier for stop loss (if mode = ATR)", Order = 402, GroupName = "4. Risk Management")]
        public double StopLossATRMultiplier { get; set; }

        [DataMember]
        [Display(Name = "Take Profit Mode", Description = "Take profit calculation mode", Order = 403, GroupName = "4. Risk Management")]
        public TakeProfitMode TakeProfitType { get; set; }

        [DataMember]
        [Display(Name = "Take Profit Ticks", Description = "Take profit in ticks (if mode = Ticks)", Order = 404, GroupName = "4. Risk Management")]
        public int TakeProfitTicks { get; set; }

        [DataMember]
        [Display(Name = "Take Profit ATR Multiplier", Description = "ATR multiplier for take profit (if mode = ATR)", Order = 405, GroupName = "4. Risk Management")]
        public double TakeProfitATRMultiplier { get; set; }

        [DataMember]
        [Display(Name = "Risk Reward Ratio", Description = "Risk:Reward ratio (if mode = RiskReward)", Order = 406, GroupName = "4. Risk Management")]
        public double RiskRewardRatio { get; set; }

        [DataMember]
        [Display(Name = "ATR Period", Description = "ATR period for calculations", Order = 407, GroupName = "4. Risk Management")]
        public int ATRPeriod { get; set; }

        [DataMember]
        [Display(Name = "Position Size", Description = "Number of contracts per trade", Order = 408, GroupName = "4. Risk Management")]
        public int PositionSize { get; set; }

        [DataMember]
        [Display(Name = "Max Daily Loss", Description = "Maximum daily loss in currency (0 = no limit)", Order = 409, GroupName = "4. Risk Management")]
        public double MaxDailyLoss { get; set; }

        [DataMember]
        [Display(Name = "Max Daily Profit", Description = "Maximum daily profit target in currency (0 = no limit)", Order = 410, GroupName = "4. Risk Management")]
        public double MaxDailyProfit { get; set; }

        [DataMember]
        [Display(Name = "Enable Trailing Stop", Description = "Enable trailing stop functionality", Order = 411, GroupName = "4. Risk Management")]
        public bool EnableTrailingStop { get; set; }
        #endregion

        #region Visual Settings Properties
        [DataMember]
        [Display(Name = "Draw Session Lines", Description = "Draw session high/low lines on chart", Order = 500, GroupName = "5. Visual Settings")]
        public bool DrawSessionLines { get; set; }

        [DataMember]
        [Display(Name = "Draw FVG Zones", Description = "Draw FVG zones on chart", Order = 501, GroupName = "5. Visual Settings")]
        public bool DrawFVGZones { get; set; }

        [DataMember]
        [Display(Name = "Draw Entry Arrows", Description = "Draw entry arrows on chart", Order = 502, GroupName = "5. Visual Settings")]
        public bool DrawEntryArrows { get; set; }

        [DataMember]
        [Display(Name = "Draw Session Backgrounds", Description = "Draw session background colors", Order = 503, GroupName = "5. Visual Settings")]
        public bool DrawSessionBackgrounds { get; set; }
        #endregion

        #region Properties
        #endregion

        #region Methods
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Session Liquidity Sweep FVG Inversion Strategy - Trades session liquidity sweeps with Fair Value Gap inversions";
                Name = "SessionLiquiditySweepFvgInversion";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
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
                BarsRequiredToTrade = 20;

                AsianStartHour = 18;
                AsianStartMinute = 0;
                AsianEndHour = 2;
                AsianEndMinute = 0;
                LondonStartHour = 2;
                LondonStartMinute = 0;
                LondonEndHour = 12;
                LondonEndMinute = 0;
                NYPMStartHour = 12;
                NYPMStartMinute = 0;
                NYPMEndHour = 18;
                NYPMEndMinute = 0;

                FVGTimeframeMinutes = 0;
                MinFVGSizeTicks = 5;
                FVGValidityBars = 20;

                EnableAsianTrades = true;
                EnableLondonTrades = true;
                EnableNYPMTrades = true;
                MaxTradesPerSession = 2;
                SweepBufferTicks = 1;

                StopLossType = StopLossMode.Ticks;
                StopLossTicks = 20;
                StopLossATRMultiplier = 1.5;
                TakeProfitType = TakeProfitMode.RiskReward;
                TakeProfitTicks = 40;
                TakeProfitATRMultiplier = 2.0;
                RiskRewardRatio = 2.0;
                ATRPeriod = 14;
                PositionSize = 1;
                MaxDailyLoss = 0;
                MaxDailyProfit = 0;
                EnableTrailingStop = false;

                DrawSessionLines = true;
                DrawFVGZones = true;
                DrawEntryArrows = true;
                DrawSessionBackgrounds = true;
            }
            else if (State == State.Configure)
            {
                if (FVGTimeframeMinutes > 0)
                {
                    AddDataSeries(BarsPeriodType.Minute, FVGTimeframeMinutes);
                    fvgBarsInProgress = 1;
                }
                else
                {
                    fvgBarsInProgress = 0;
                }
            }
            else if (State == State.DataLoaded)
            {
                activeFVGs = new List<FairValueGap>();
                atr = ATR(ATRPeriod);
                ClearOutputWindow();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade)
                return;

            if (BarsInProgress == 0)
            {
                CheckDailyReset();
                if (CheckDailyLimits())
                    return;

                UpdateSessions();
                CheckForLiquiditySweeps();
                ManageActiveFVGs();
                CheckForEntries();
            }

            if (fvgBarsInProgress == 0 || BarsInProgress == fvgBarsInProgress)
            {
                if (CurrentBar >= 3)
                {
                    DetectFVGs();
                }
            }
        }

        private void CheckDailyReset()
        {
            if (Time[0].Date != lastTradingDay.Date)
            {
                dailyProfitLoss = 0;
                lastTradingDay = Time[0];
            }
            else
            {
                if (Position.MarketPosition == MarketPosition.Flat && SystemPerformance != null && SystemPerformance.AllTrades.Count > 0)
                {
                    var todayTrades = SystemPerformance.AllTrades.Where(t => t.Exit.Time.Date == Time[0].Date);
                    dailyProfitLoss = todayTrades.Sum(t => t.ProfitCurrency);
                }
            }
        }

        private bool CheckDailyLimits()
        {
            if (MaxDailyLoss > 0 && dailyProfitLoss <= -MaxDailyLoss)
            {
                Print(string.Format("{0} - Max daily loss reached: {1:C}", Time[0], dailyProfitLoss));
                return true;
            }

            if (MaxDailyProfit > 0 && dailyProfitLoss >= MaxDailyProfit)
            {
                Print(string.Format("{0} - Max daily profit reached: {1:C}", Time[0], dailyProfitLoss));
                return true;
            }

            return false;
        }

        private void UpdateSessions()
        {
            DateTime currentTime = Time[0];
            TimeSpan timeOfDay = currentTime.TimeOfDay;

            UpdateSession(ref currentAsianSession, ref previousAsianSession, SessionType.Asian, 
                new TimeSpan(AsianStartHour, AsianStartMinute, 0), new TimeSpan(AsianEndHour, AsianEndMinute, 0));
            UpdateSession(ref currentLondonSession, ref previousLondonSession, SessionType.London, 
                new TimeSpan(LondonStartHour, LondonStartMinute, 0), new TimeSpan(LondonEndHour, LondonEndMinute, 0));
            UpdateSession(ref currentNYPMSession, ref previousNYPMSession, SessionType.NYPM, 
                new TimeSpan(NYPMStartHour, NYPMStartMinute, 0), new TimeSpan(NYPMEndHour, NYPMEndMinute, 0));
        }

        private void UpdateSession(ref SessionData currentSession, ref SessionData previousSession, 
            SessionType type, TimeSpan startTime, TimeSpan endTime)
        {
            DateTime currentTime = Time[0];
            TimeSpan timeOfDay = currentTime.TimeOfDay;

            bool inSession = IsInSession(timeOfDay, startTime, endTime);

            if (inSession)
            {
                if (currentSession == null || currentSession.IsComplete)
                {
                    if (currentSession != null && !currentSession.IsComplete)
                    {
                        currentSession.IsComplete = true;
                        previousSession = currentSession;
                    }

                    DateTime sessionStart = currentTime.Date + startTime;
                    DateTime sessionEnd = currentTime.Date + endTime;
                    if (endTime < startTime)
                    {
                        if (timeOfDay < endTime)
                            sessionStart = sessionStart.AddDays(-1);
                        else
                            sessionEnd = sessionEnd.AddDays(1);
                    }

                    currentSession = new SessionData(type, sessionStart, sessionEnd);
                }

                if (High[0] > currentSession.High)
                    currentSession.High = High[0];
                if (Low[0] < currentSession.Low)
                    currentSession.Low = Low[0];
            }
            else
            {
                if (currentSession != null && !currentSession.IsComplete)
                {
                    currentSession.IsComplete = true;
                    previousSession = currentSession;

                    if (DrawSessionLines && previousSession != null)
                    {
                        DrawSessionHighLow(previousSession);
                    }
                }
            }

            if (DrawSessionBackgrounds && currentSession != null && !currentSession.IsComplete)
            {
                DrawSessionBackground(currentSession, CurrentBar);
            }
        }

        private bool IsInSession(TimeSpan current, TimeSpan start, TimeSpan end)
        {
            if (start < end)
            {
                return current >= start && current < end;
            }
            else
            {
                return current >= start || current < end;
            }
        }

        private void CheckForLiquiditySweeps()
        {
            if (previousAsianSession != null && !previousAsianSession.HighSwept && EnableAsianTrades)
            {
                if (High[0] >= previousAsianSession.High + (SweepBufferTicks * TickSize))
                {
                    previousAsianSession.HighSwept = true;
                    OnLiquiditySweep(previousAsianSession, true);
                }
            }

            if (previousAsianSession != null && !previousAsianSession.LowSwept && EnableAsianTrades)
            {
                if (Low[0] <= previousAsianSession.Low - (SweepBufferTicks * TickSize))
                {
                    previousAsianSession.LowSwept = true;
                    OnLiquiditySweep(previousAsianSession, false);
                }
            }

            if (previousLondonSession != null && !previousLondonSession.HighSwept && EnableLondonTrades)
            {
                if (High[0] >= previousLondonSession.High + (SweepBufferTicks * TickSize))
                {
                    previousLondonSession.HighSwept = true;
                    OnLiquiditySweep(previousLondonSession, true);
                }
            }

            if (previousLondonSession != null && !previousLondonSession.LowSwept && EnableLondonTrades)
            {
                if (Low[0] <= previousLondonSession.Low - (SweepBufferTicks * TickSize))
                {
                    previousLondonSession.LowSwept = true;
                    OnLiquiditySweep(previousLondonSession, false);
                }
            }

            if (previousNYPMSession != null && !previousNYPMSession.HighSwept && EnableNYPMTrades)
            {
                if (High[0] >= previousNYPMSession.High + (SweepBufferTicks * TickSize))
                {
                    previousNYPMSession.HighSwept = true;
                    OnLiquiditySweep(previousNYPMSession, true);
                }
            }

            if (previousNYPMSession != null && !previousNYPMSession.LowSwept && EnableNYPMTrades)
            {
                if (Low[0] <= previousNYPMSession.Low - (SweepBufferTicks * TickSize))
                {
                    previousNYPMSession.LowSwept = true;
                    OnLiquiditySweep(previousNYPMSession, false);
                }
            }
        }

        private void OnLiquiditySweep(SessionData session, bool isHighSweep)
        {
            Print(string.Format("{0} - Liquidity Sweep Detected: {1} {2} @ {3}", 
                Time[0], session.Type, isHighSweep ? "HIGH" : "LOW", isHighSweep ? session.High : session.Low));
        }

        private void DetectFVGs()
        {
            int barsAgo = fvgBarsInProgress;
            
            if (CurrentBar < 3)
                return;

            bool bullishFVG = Low[0] > High[2];
            bool bearishFVG = High[0] < Low[2];

            if (bullishFVG)
            {
                double gapBottom = High[2];
                double gapTop = Low[0];
                double gapSizeTicks = (gapTop - gapBottom) / TickSize;

                if (gapSizeTicks >= MinFVGSizeTicks)
                {
                    SessionData relevantSession = GetRelevantSession();
                    if (relevantSession != null && relevantSession.HighSwept)
                    {
                        FairValueGap fvg = new FairValueGap(true, gapTop, gapBottom, CurrentBar, 
                            Time[0], relevantSession.Type, true);
                        activeFVGs.Add(fvg);

                        Print(string.Format("{0} - Bullish FVG Detected: Top={1}, Bottom={2}, Size={3} ticks", 
                            Time[0], gapTop, gapBottom, gapSizeTicks));

                        if (DrawFVGZones)
                        {
                            DrawFVGZone(fvg, CurrentBar);
                        }
                    }
                }
            }

            if (bearishFVG)
            {
                double gapTop = Low[2];
                double gapBottom = High[0];
                double gapSizeTicks = (gapTop - gapBottom) / TickSize;

                if (gapSizeTicks >= MinFVGSizeTicks)
                {
                    SessionData relevantSession = GetRelevantSession();
                    if (relevantSession != null && relevantSession.LowSwept)
                    {
                        FairValueGap fvg = new FairValueGap(false, gapTop, gapBottom, CurrentBar, 
                            Time[0], relevantSession.Type, false);
                        activeFVGs.Add(fvg);

                        Print(string.Format("{0} - Bearish FVG Detected: Top={1}, Bottom={2}, Size={3} ticks", 
                            Time[0], gapTop, gapBottom, gapSizeTicks));

                        if (DrawFVGZones)
                        {
                            DrawFVGZone(fvg, CurrentBar);
                        }
                    }
                }
            }
        }

        private void ManageActiveFVGs()
        {
            if (activeFVGs == null || activeFVGs.Count == 0)
                return;

            List<FairValueGap> fvgsToRemove = new List<FairValueGap>();

            foreach (var fvg in activeFVGs)
            {
                if (FVGValidityBars > 0 && CurrentBar - fvg.BarIndex > FVGValidityBars)
                {
                    fvgsToRemove.Add(fvg);
                    continue;
                }

                if (!fvg.IsInverted)
                {
                    if (fvg.IsBullish)
                    {
                        if (Close[0] < fvg.GapBottom)
                        {
                            fvg.IsInverted = true;
                            Print(string.Format("{0} - Bullish FVG Inverted at {1}", Time[0], Close[0]));
                        }
                    }
                    else
                    {
                        if (Close[0] > fvg.GapTop)
                        {
                            fvg.IsInverted = true;
                            Print(string.Format("{0} - Bearish FVG Inverted at {1}", Time[0], Close[0]));
                        }
                    }
                }
            }

            foreach (var fvg in fvgsToRemove)
            {
                activeFVGs.Remove(fvg);
            }
        }

        private void CheckForEntries()
        {
            if (Position.MarketPosition != MarketPosition.Flat)
                return;

            if (activeFVGs == null || activeFVGs.Count == 0)
                return;

            foreach (var fvg in activeFVGs.Where(f => f.IsInverted))
            {
                SessionData session = GetSessionByType(fvg.AssociatedSession);
                
                if (session == null)
                    continue;

                if (MaxTradesPerSession > 0 && session.TradesCount >= MaxTradesPerSession)
                    continue;

                if (fvg.IsBullish && fvg.IsHighSweep)
                {
                    ExecuteShortEntry(fvg, session);
                    activeFVGs.Remove(fvg);
                    break;
                }
                else if (!fvg.IsBullish && !fvg.IsHighSweep)
                {
                    ExecuteLongEntry(fvg, session);
                    activeFVGs.Remove(fvg);
                    break;
                }
            }
        }

        private void ExecuteLongEntry(FairValueGap fvg, SessionData session)
        {
            string signalName = string.Format("Long_{0}_{1}", session.Type, Time[0].ToString("HHmmss"));
            
            double stopLoss = CalculateStopLoss(false);
            double takeProfit = CalculateTakeProfit(false, stopLoss);

            EnterLong(PositionSize, signalName);
            SetStopLoss(signalName, CalculationMode.Ticks, stopLoss / TickSize, false);
            SetProfitTarget(signalName, CalculationMode.Ticks, takeProfit / TickSize);

            session.TradesCount++;

            Print(string.Format("{0} - LONG Entry: {1}, SL={2} ticks, TP={3} ticks", 
                Time[0], signalName, stopLoss / TickSize, takeProfit / TickSize));
        }

        private void ExecuteShortEntry(FairValueGap fvg, SessionData session)
        {
            string signalName = string.Format("Short_{0}_{1}", session.Type, Time[0].ToString("HHmmss"));
            
            double stopLoss = CalculateStopLoss(true);
            double takeProfit = CalculateTakeProfit(true, stopLoss);

            EnterShort(PositionSize, signalName);
            SetStopLoss(signalName, CalculationMode.Ticks, stopLoss / TickSize, false);
            SetProfitTarget(signalName, CalculationMode.Ticks, takeProfit / TickSize);

            session.TradesCount++;

            Print(string.Format("{0} - SHORT Entry: {1}, SL={2} ticks, TP={3} ticks", 
                Time[0], signalName, stopLoss / TickSize, takeProfit / TickSize));
        }

        private double CalculateStopLoss(bool isShort)
        {
            double sl = 0;

            switch (StopLossType)
            {
                case StopLossMode.Ticks:
                    sl = StopLossTicks * TickSize;
                    break;
                case StopLossMode.ATR:
                    if (atr != null && atr[0] > 0)
                    {
                        sl = atr[0] * StopLossATRMultiplier;
                    }
                    else
                    {
                        sl = StopLossTicks * TickSize;
                    }
                    break;
            }

            return sl;
        }

        private double CalculateTakeProfit(bool isShort, double stopLoss)
        {
            double tp = 0;

            switch (TakeProfitType)
            {
                case TakeProfitMode.Ticks:
                    tp = TakeProfitTicks * TickSize;
                    break;
                case TakeProfitMode.ATR:
                    if (atr != null && atr[0] > 0)
                    {
                        tp = atr[0] * TakeProfitATRMultiplier;
                    }
                    else
                    {
                        tp = TakeProfitTicks * TickSize;
                    }
                    break;
                case TakeProfitMode.RiskReward:
                    tp = stopLoss * RiskRewardRatio;
                    break;
            }

            return tp;
        }

        private SessionData GetRelevantSession()
        {
            if (previousAsianSession != null && previousAsianSession.HighSwept && !previousAsianSession.LowSwept)
                return previousAsianSession;
            if (previousAsianSession != null && previousAsianSession.LowSwept && !previousAsianSession.HighSwept)
                return previousAsianSession;
            if (previousLondonSession != null && previousLondonSession.HighSwept && !previousLondonSession.LowSwept)
                return previousLondonSession;
            if (previousLondonSession != null && previousLondonSession.LowSwept && !previousLondonSession.HighSwept)
                return previousLondonSession;
            if (previousNYPMSession != null && previousNYPMSession.HighSwept && !previousNYPMSession.LowSwept)
                return previousNYPMSession;
            if (previousNYPMSession != null && previousNYPMSession.LowSwept && !previousNYPMSession.HighSwept)
                return previousNYPMSession;

            return null;
        }

        private SessionData GetSessionByType(SessionType type)
        {
            switch (type)
            {
                case SessionType.Asian:
                    return previousAsianSession;
                case SessionType.London:
                    return previousLondonSession;
                case SessionType.NYPM:
                    return previousNYPMSession;
                default:
                    return null;
            }
        }

        private void DrawSessionHighLow(SessionData session)
        {
        }

        private void DrawSessionBackground(SessionData session, int currentBar)
        {
        }

        private void DrawFVGZone(FairValueGap fvg, int barIndex)
        {
        }
        #endregion
    }
}
