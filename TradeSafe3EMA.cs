#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators
{
	public class TradeSafe3EMA : Indicator
	{
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"EMA crossover indicator";
				Name										= "TradeSafe3EMA";
				Calculate									= Calculate.OnPriceChange;
				IsOverlay									= true;
				DisplayInDataBox							= false;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event.
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;
			}
			else if (State == State.Configure)
			{
				var m21 = new Stroke(Brushes.Magenta, 2);
				m21.DashStyleHelper = DashStyleHelper.Dash;
				AddPlot(new Stroke(Brushes.Red, 2), PlotStyle.Line, "EMA7");
				AddPlot(new Stroke(Brushes.Blue, 2), PlotStyle.Line, "EMA13");
				AddPlot(m21, PlotStyle.Line, "EMA21");
			}
		}

		protected override void OnBarUpdate()
		{
            EMA7[0]  = EMA(7)[0];
            EMA13[0] = EMA(13)[0];
            EMA21[0] = EMA(21)[0];
		}

        #region Properties
        [Browsable(false)]	// this line prevents the data series from being displayed in the indicator properties dialog, do not remove
        [XmlIgnore()]		// this line ensures that the indicator can be saved/recovered as part of a chart template, do not remove
        public Series<double> EMA7
        {
            get { return Values[0]; }
        }

		[Browsable(false)]	// this line prevents the data series from being displayed in the indicator properties dialog, do not remove
        [XmlIgnore()]		// this line ensures that the indicator can be saved/recovered as part of a chart template, do not remove
        public Series<double> EMA13
        {
            get { return Values[1]; }
        }

		[Browsable(false)]	// this line prevents the data series from being displayed in the indicator properties dialog, do not remove
        [XmlIgnore()]		// this line ensures that the indicator can be saved/recovered as part of a chart template, do not remove
        public Series<double> EMA21
        {
            get { return Values[2]; }
        }

        #endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private TradeSafe3EMA[] cacheTradeSafe3EMA;
		public TradeSafe3EMA TradeSafe3EMA()
		{
			return TradeSafe3EMA(Input);
		}

		public TradeSafe3EMA TradeSafe3EMA(ISeries<double> input)
		{
			if (cacheTradeSafe3EMA != null)
				for (int idx = 0; idx < cacheTradeSafe3EMA.Length; idx++)
					if (cacheTradeSafe3EMA[idx] != null &&  cacheTradeSafe3EMA[idx].EqualsInput(input))
						return cacheTradeSafe3EMA[idx];
			return CacheIndicator<TradeSafe3EMA>(new TradeSafe3EMA(), input, ref cacheTradeSafe3EMA);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.TradeSafe3EMA TradeSafe3EMA()
		{
			return indicator.TradeSafe3EMA(Input);
		}

		public Indicators.TradeSafe3EMA TradeSafe3EMA(ISeries<double> input )
		{
			return indicator.TradeSafe3EMA(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.TradeSafe3EMA TradeSafe3EMA()
		{
			return indicator.TradeSafe3EMA(Input);
		}

		public Indicators.TradeSafe3EMA TradeSafe3EMA(ISeries<double> input )
		{
			return indicator.TradeSafe3EMA(input);
		}
	}
}

#endregion
