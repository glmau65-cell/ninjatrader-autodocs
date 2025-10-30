using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Strategies
{
	[Serializable]
	[DataContract]
	public class SevenPMBreakoutStrategy : Strategy
	{
		private double referenceHigh;
		private double referenceLow;
		private DateTime referenceDate;
		private bool stoppedOut;
		private int tradesCountToday;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"Strategy that trades breakouts based on 7PM reference levels";
				Name = "SevenPMBreakoutStrategy";
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
				BarsRequiredToTrade = 20;
				IsInstantiatedOnEachOptimizationIteration = true;

				ReferenceHour = 19;
				ReferenceMinute = 0;
				ResetDaily = true;
				MaxTradesPerDay = 1;
				StopLossTicks = 10;
				TakeProfit1Ticks = 15;
				TakeProfit2Ticks = 25;
			}
			else if (State == State.Configure)
			{
			}
			else if (State == State.DataLoaded)
			{
				referenceHigh = double.MinValue;
				referenceLow = double.MaxValue;
				referenceDate = DateTime.MinValue;
				stoppedOut = false;
				tradesCountToday = 0;
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < BarsRequiredToTrade)
				return;

			if (Time[0].Hour == ReferenceHour && Time[0].Minute == ReferenceMinute)
			{
				SetReferenceLevel();
			}

			if (ResetDaily && referenceDate.Date != Time[0].Date)
			{
				ResetReferenceState();
			}

			if (Position.MarketPosition == MarketPosition.Flat)
			{
				CheckEntryConditions();
			}
		}

		private void SetReferenceLevel()
		{
			referenceHigh = High[0];
			referenceLow = Low[0];
			referenceDate = Time[0].Date;
			Print(string.Format("{0} - Reference levels set: High={1}, Low={2}", Time[0], referenceHigh, referenceLow));
		}

		private void ResetReferenceState()
		{
			referenceHigh = double.MinValue;
			referenceLow = double.MaxValue;
			stoppedOut = false;
			tradesCountToday = 0;
			Print(string.Format("{0} - Reference state reset for new day", Time[0]));
		}

		private void CheckEntryConditions()
		{
			if (referenceHigh == double.MinValue || referenceLow == double.MaxValue)
				return;

			if (stoppedOut)
				return;

			if (tradesCountToday >= MaxTradesPerDay)
			{
				Print(string.Format("{0} - Daily trade limit reached", Time[0]));
				return;
			}

			if (Close[0] > referenceHigh)
			{
				EnterLong();
				tradesCountToday++;
				Print(string.Format("{0} - Trade #{1} of {2} taken", Time[0], tradesCountToday, MaxTradesPerDay));
			}
			else if (Close[0] < referenceLow)
			{
				EnterShort();
				tradesCountToday++;
				Print(string.Format("{0} - Trade #{1} of {2} taken", Time[0], tradesCountToday, MaxTradesPerDay));
			}
		}

		protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
		{
			if (execution.Order != null && execution.Order.OrderState == OrderState.Filled)
			{
				if (execution.Order.Name.Contains("Stop"))
				{
					stoppedOut = true;
					Print(string.Format("{0} - Stopped out, no more trades today", time));
				}
			}
		}

		#region Properties

		[NinjaScriptProperty]
		[Range(0, 23)]
		[Display(Name = "Reference Hour", Description = "Hour to capture reference high/low (0-23)", Order = 1, GroupName = "1. Reference Setup")]
		public int ReferenceHour { get; set; }

		[NinjaScriptProperty]
		[Range(0, 59)]
		[Display(Name = "Reference Minute", Description = "Minute to capture reference high/low (0-59)", Order = 2, GroupName = "1. Reference Setup")]
		public int ReferenceMinute { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Reset Daily", Description = "Reset reference levels daily", Order = 3, GroupName = "1. Reference Setup")]
		public bool ResetDaily { get; set; }

		[NinjaScriptProperty]
		[Range(1, 10)]
		[Display(Name = "Max Trades Per Day", Description = "Maximum number of trades allowed per day", Order = 4, GroupName = "1. Reference Setup")]
		public int MaxTradesPerDay { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Stop Loss (Ticks)", Description = "Stop loss in ticks", Order = 1, GroupName = "2. Risk Management")]
		public int StopLossTicks { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Take Profit 1 (Ticks)", Description = "First profit target in ticks", Order = 2, GroupName = "2. Risk Management")]
		public int TakeProfit1Ticks { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Take Profit 2 (Ticks)", Description = "Second profit target in ticks", Order = 3, GroupName = "2. Risk Management")]
		public int TakeProfit2Ticks { get; set; }

		#endregion
	}
}
