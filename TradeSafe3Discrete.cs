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
using TradeSafe3;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators
{
	public class TradeSafe3Discrete : TradeSafe3Headless
	{
		
		protected override void OnStateChange()
		{
			base.OnStateChange();
			
			if (State == State.SetDefaults)
			{
				Description	= @"Trend in terms of discrete values";
				Name		= "TradeSafe3Discrete";
				AddPlot(new Stroke(Brushes.Red, 1), PlotStyle.Line, "Trend");
			}
		}

		protected override void OnBarUpdate()
		{			
			base.OnBarUpdate();
			Value[0] = Trend[0] == Direction.Up
				? 1
				: Trend[0] == Direction.Down
					? -1
					: 0;
		}
		
	}
}


