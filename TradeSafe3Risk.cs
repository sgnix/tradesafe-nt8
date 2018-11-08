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
	public class TradeSafe3Risk : Indicator
	{
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Risk, Range and Rule21";
				Name										= "TradeSafe3RRR";
				Calculate									= Calculate.OnPriceChange;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;

                //Disable this property if your indicator requires custom
                //values that cumulate with each new market data event.  See
                //Help Guide for additional information.
				IsSuspendedWhileInactive					= true;

                // Configure plots here so they show in the dialog, which is
                // needed in order to add this to the Market Analyzer.
                //
                // Each call to AddPlot adds a new Series<double> object to the
                // Values collection.  The collections can be referenced by
                // their index of creation, e.g.  Values[0] is the first
                // created plot and it's of type Series<double>
				AddPlot(new Stroke(Brushes.Red, 1), PlotStyle.Line, "Range");
				AddPlot(new Stroke(Brushes.Blue, 1), PlotStyle.Line, "Risk");
				AddPlot(new Stroke(Brushes.Magenta, 2), PlotStyle.Line, "Rule21");
			}
		}

		protected override void OnBarUpdate()
		{
			// Range
			Range[0] = Math.Round(Math.Abs(High[0] - Low[0]) / TickSize);

			// Risk
			Risk[0] = ((Range[0] + 2) * Instrument.MasterInstrument.PointValue * TickSize);

			// Rule21
			double ma = EMA(21)[0];
			double up = (Math.Max(ma - High[0], 0) / TickSize);
			double down = (Math.Max(Low[0] - ma, 0) / TickSize);
			Rule21[0] = Math.Round(Math.Max(up, down));
		}

        #region Properties
        [Browsable(false)]	// this line prevents the data series from being displayed in the indicator properties dialog, do not remove
        [XmlIgnore()]		// this line ensures that the indicator can be saved/recovered as part of a chart template, do not remove
        public Series<double> Range
        {
            get { return Values[0]; }
        }

		[Browsable(false)]	// this line prevents the data series from being displayed in the indicator properties dialog, do not remove
        [XmlIgnore()]		// this line ensures that the indicator can be saved/recovered as part of a chart template, do not remove
        public Series<double> Risk
        {
            get { return Values[1]; }
        }

		[Browsable(false)]	// this line prevents the data series from being displayed in the indicator properties dialog, do not remove
        [XmlIgnore()]		// this line ensures that the indicator can be saved/recovered as part of a chart template, do not remove
        public Series<double> Rule21
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
		private TradeSafe3Risk[] cacheTradeSafe3Risk;
		public TradeSafe3Risk TradeSafe3Risk()
		{
			return TradeSafe3Risk(Input);
		}

		public TradeSafe3Risk TradeSafe3Risk(ISeries<double> input)
		{
			if (cacheTradeSafe3Risk != null)
				for (int idx = 0; idx < cacheTradeSafe3Risk.Length; idx++)
					if (cacheTradeSafe3Risk[idx] != null &&  cacheTradeSafe3Risk[idx].EqualsInput(input))
						return cacheTradeSafe3Risk[idx];
			return CacheIndicator<TradeSafe3Risk>(new TradeSafe3Risk(), input, ref cacheTradeSafe3Risk);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.TradeSafe3Risk TradeSafe3Risk()
		{
			return indicator.TradeSafe3Risk(Input);
		}

		public Indicators.TradeSafe3Risk TradeSafe3Risk(ISeries<double> input )
		{
			return indicator.TradeSafe3Risk(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.TradeSafe3Risk TradeSafe3Risk()
		{
			return indicator.TradeSafe3Risk(Input);
		}

		public Indicators.TradeSafe3Risk TradeSafe3Risk(ISeries<double> input )
		{
			return indicator.TradeSafe3Risk(input);
		}
	}
}

#endregion
