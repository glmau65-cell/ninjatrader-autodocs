using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

[Serializable]
[DataContract]
namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class SessionLiquiditySweepFvgInversion : NinjaTrader.NinjaScript.Strategies.Strategy
    {
        #region Variables
        private double sessionHigh = double.MinValue;
        private double sessionLow = double.MaxValue;
        private bool highSwept = false;
        private bool lowSwept = false;
        private List<double> fvgTops;
        private List<double> fvgBottoms;
        private List<bool> fvgIsBullish;
        #endregion

        #region Properties
        [DataMember]
        [Display(Name = "Session Start Hour", Order = 1, GroupName = "Session")]
        public int SessionStartHour { get; set; }

        [DataMember]
        [Display(Name = "Session End Hour", Order = 2, GroupName = "Session")]
        public int SessionEndHour { get; set; }

        [DataMember]
        [Display(Name = "Min FVG Size (Ticks)", Order = 3, GroupName = "FVG")]
        public int MinFVGSizeTicks { get; set; }

        [DataMember]
        [Display(Name = "Stop Loss (Ticks)", Order = 4, GroupName = "Risk")]
        public int StopLossTicks { get; set; }

        [DataMember]
        [Display(Name = "Take Profit (Ticks)", Order = 5, GroupName = "Risk")]
        public int TakeProfitTicks { get; set; }

        [DataMember]
        [Display(Name = "Quantity", Order = 6, GroupName = "Risk")]
        public int Quantity { get; set; }
        #endregion

        #region Methods
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Session Liquidity Sweep FVG Inversion Strategy";
                Name = "SessionLiquiditySweepFvgInversion";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = false;
                IsFillLimitOnTouch = false;
                StartBehavior = StartBehavior.WaitUntilFlat;
                BarsRequiredToTrade = 20;

                SessionStartHour = 2;
                SessionEndHour = 12;
                MinFVGSizeTicks = 5;
                StopLossTicks = 20;
                TakeProfitTicks = 40;
                Quantity = 1;
            }
            else if (State == State.DataLoaded)
            {
                fvgTops = new List<double>();
                fvgBottoms = new List<double>();
                fvgIsBullish = new List<bool>();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade)
                return;

            UpdateSession();
            CheckLiquiditySweeps();
            DetectFVGs();
            CheckEntries();
        }

        private void UpdateSession()
        {
            int hour = Time[0].Hour;
            
            if (hour == SessionStartHour && Time[1].Hour != SessionStartHour)
            {
                sessionHigh = High[0];
                sessionLow = Low[0];
                highSwept = false;
                lowSwept = false;
                fvgTops.Clear();
                fvgBottoms.Clear();
                fvgIsBullish.Clear();
            }
            else if (hour >= SessionStartHour && hour < SessionEndHour)
            {
                if (High[0] > sessionHigh)
                    sessionHigh = High[0];
                if (Low[0] < sessionLow)
                    sessionLow = Low[0];
            }
        }

        private void CheckLiquiditySweeps()
        {
            if (!highSwept && High[0] > sessionHigh)
            {
                highSwept = true;
            }
            
            if (!lowSwept && Low[0] < sessionLow)
            {
                lowSwept = true;
            }
        }

        private void DetectFVGs()
        {
            if (CurrentBar < 3)
                return;

            bool bullishFVG = Low[0] > High[2];
            bool bearishFVG = High[0] < Low[2];

            if (bullishFVG && highSwept)
            {
                double gapSize = (Low[0] - High[2]) / TickSize;
                if (gapSize >= MinFVGSizeTicks)
                {
                    fvgTops.Add(Low[0]);
                    fvgBottoms.Add(High[2]);
                    fvgIsBullish.Add(true);
                }
            }

            if (bearishFVG && lowSwept)
            {
                double gapSize = (Low[2] - High[0]) / TickSize;
                if (gapSize >= MinFVGSizeTicks)
                {
                    fvgTops.Add(Low[2]);
                    fvgBottoms.Add(High[0]);
                    fvgIsBullish.Add(false);
                }
            }
        }

        private void CheckEntries()
        {
            if (Position.MarketPosition != MarketPosition.Flat)
                return;

            for (int i = fvgTops.Count - 1; i >= 0; i--)
            {
                if (fvgIsBullish[i] && Close[0] < fvgBottoms[i])
                {
                    EnterShort(Quantity, "FVG_Short");
                    SetStopLoss("FVG_Short", CalculationMode.Ticks, StopLossTicks, false);
                    SetProfitTarget("FVG_Short", CalculationMode.Ticks, TakeProfitTicks);
                    fvgTops.RemoveAt(i);
                    fvgBottoms.RemoveAt(i);
                    fvgIsBullish.RemoveAt(i);
                    break;
                }
                else if (!fvgIsBullish[i] && Close[0] > fvgTops[i])
                {
                    EnterLong(Quantity, "FVG_Long");
                    SetStopLoss("FVG_Long", CalculationMode.Ticks, StopLossTicks, false);
                    SetProfitTarget("FVG_Long", CalculationMode.Ticks, TakeProfitTicks);
                    fvgTops.RemoveAt(i);
                    fvgBottoms.RemoveAt(i);
                    fvgIsBullish.RemoveAt(i);
                    break;
                }
            }
        }
        #endregion
    }
}
