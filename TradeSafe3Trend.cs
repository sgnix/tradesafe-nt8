#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using System.Speech.Synthesis;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace TradeSafe3
{
	#region VoiceConverter
	public class VoiceConverter : TypeConverter
	{

		public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
		{
			if (context == null)
				return null;

			List<string> list = new List<string>();
			var s = new SpeechSynthesizer();

			foreach (InstalledVoice v in s.GetInstalledVoices())
			{
				list.Add(v.VoiceInfo.Name);
			}

			return new TypeConverter.StandardValuesCollection(list);
		}

		public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
		{
			return true;
		}
	}
	#endregion
}

//This namespace holds Indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators
{
    public class TradeSafe3Trend : TradeSafe3Headless
	{
		enum AlertType { None, Reversal, TrendLost, Congestion, Breakout, Reset, Fakeout, Confirmed, Canceled};

        #region Constants
		// Tagnames
		const string boxTag   = "congestion";
		const string refTag   = "refbar";

		// Strings
		const string strParameters = "Parameters";
		const int maxLastBar = 60;

        #endregion

        #region Variables

		// Last breakout method saved
		TradeSafe3.BreakoutMethod lastMethod = TradeSafe3.BreakoutMethod.None;

		// The bar where the trend changed
		int trendChangeBar = -1;

		// Alerts and speech
		SpeechSynthesizer synth = new SpeechSynthesizer();
		Dictionary<AlertType, string> alerts;

        #endregion

		#region OnStateChange
		protected override void OnStateChange()
		{
            base.OnStateChange();

			if (State == State.SetDefaults)
			{
				Description	= @"An implementation of Michael Guess' TradeSafe trend change indicator.";
				Name	   	= "TradeSafe3Trend";
				BoxBrush 	= Brushes.Blue;
				TextAlerts 	= true;
				SoundAlerts = true;
				LastOnly    = true;
				Voice       = synth.Voice.Name;
				VendorLicense("TradeSafe", Name, "http://daytradesafe.com/", "mguess@daytradesafe.com");
			}
			else if (State == State.Configure)
			{
                alerts = new Dictionary<AlertType, string>();
			}
            else if (State == State.DataLoaded)
            {
                CreateWaveFiles();
            }
		}
		#endregion

		#region OnBarUpdate
		protected override void OnBarUpdate()
		{
			if (LastOnly && Bars.Count - CurrentBar > maxLastBar)
				return;

			base.OnBarUpdate();

			// This method is processed on each tick (not on bar close).
			// Because of that, we need to prevent the trend color overwrite
			// the trend change color. This is where trendCahngeBar comes in
			// play. It remembers if the current bar was a trend change bar
			// and doesn't allow the trenc color to paint over it.
			if (lastMethod == TradeSafe3.BreakoutMethod.Crossover)
				BackBrush = Brushes.Crimson;
			else if (lastMethod == TradeSafe3.BreakoutMethod.Swing)
				BackBrush = Brushes.SlateGray;
			else if (lastMethod == TradeSafe3.BreakoutMethod.Both)
				BackBrush = Brushes.Black;
			else if (lastMethod == TradeSafe3.BreakoutMethod.Lost)
				BackBrush = Brushes.Teal;
			else if (trendChangeBar != CurrentBar && Trend[0] == TradeSafe3.Direction.Up)
				BackBrush = Brushes.LightGreen;
			else if (trendChangeBar != CurrentBar && Trend[0] == TradeSafe3.Direction.Down)
				BackBrush = Brushes.LightPink;

			lastMethod = TradeSafe3.BreakoutMethod.None;

			if (Box.State != TradeSafe3.State.None && Box.State != TradeSafe3.State.Canceled)
				DrawMainBox();
		}
		#endregion

		#region CreateWaveFiles
		void CreateWaveFiles()
		{
			alerts.Add(AlertType.Congestion, "new congestion");
			alerts.Add(AlertType.Reversal,   "trend reversal");
			alerts.Add(AlertType.TrendLost,  "trend lost");
			alerts.Add(AlertType.Breakout,   "breakout");
			alerts.Add(AlertType.Reset,      "breakout reset");
			alerts.Add(AlertType.Fakeout,    "breakout fakeout");
			alerts.Add(AlertType.Confirmed,  "breakout confirmed");
			alerts.Add(AlertType.Canceled,   "congestion canceled");

			synth.SelectVoice(Voice);

			foreach(KeyValuePair<AlertType, string> alert in alerts)
			{
				var filename = AlertFilename(alert.Key);

				if (!System.IO.File.Exists(filename))
				{
					synth.SetOutputToWaveFile(filename);
					synth.Speak(AlertText(alert.Key));
					synth.SetOutputToNull();
				}
			}
		}
		#endregion

		#region AlertFilename
		string AlertFilename(AlertType key)
		{
			return String.Format(@"{0}{1}-{2}{3}-{4}-{5}.wav",
				System.IO.Path.GetTempPath(),
				Instrument.MasterInstrument.Name,
				BarsPeriod.Value,
				BarsPeriod.BaseBarsPeriodType,
				synth.Voice.Id,
				key);
		}
		#endregion

		#region AlertText
		string AlertText(AlertType key)
		{
			return String.Format("{0}, {1} - {2}", Instrument.MasterInstrument.Name, BarsPeriod, alerts[key]);
		}
		#endregion

		#region Virtual
		protected override void OnTrendChange()
		{
			lastMethod = Method;
			trendChangeBar = CurrentBar;
			Announce(Trend[0] == TradeSafe3.Direction.None ? AlertType.TrendLost : AlertType.Reversal);
		}

		protected override void OnNewCongestion()
		{
			Announce(AlertType.Congestion);
			DrawShadowBox();
		}

		protected override void OnBreakout()
		{
			Announce(AlertType.Breakout);
		}

		protected override void OnBreakoutReset()
		{
			Announce(AlertType.Reset);
		}

		protected override void OnBreakoutFakeout()
		{
			Announce(AlertType.Fakeout);
		}

		protected override void OnBreakoutConfirmed()
		{
			Announce(AlertType.Confirmed);
		}

		protected override void OnCongestionCanceled()
		{
			Announce(AlertType.Canceled);
			DrawShadowBox();

			RemoveDrawObject(refTag);
			RemoveDrawObject(boxTag);

		}
		#endregion

		#region DrawMainBox
		void DrawMainBox()
		{
			if (PlotBox)
			{
				int    x1 = CurrentBar - Box.StartBar;
				double y1 = Box.StartY;
				int    x2 = CurrentBar - Box.EndBar;
				double y2 = Box.EndY;
				int    r  = CurrentBar - Box.ReferenceBar; // box reference bar

				// Draw the current box and the diamond on top of the reference bar
				Draw.Diamond(this, refTag, false, r, High[r] + TickSize, BoxBrush, true);
				Rectangle re = Draw.Rectangle(this, boxTag, true, x1, y1, x2, y2, Brushes.Transparent, BoxBrush, 30, true);
				re.OutlineStroke.Width = 1;
			}
		}
		#endregion

		#region DrawShadowBox
        void DrawShadowBox()
        {
            if (!PlotBox) return;
            var r = (Rectangle)DrawObjects[boxTag];
            if (r != null)
            {
                var a = r.StartAnchor;
                var b = r.EndAnchor;
                string shadowTag   = String.Format("shadow_{0}_{1}", a.DrawnOnBar, b.DrawnOnBar);
                Rectangle rc = Draw.Rectangle(this, shadowTag, true, a.BarsAgo, a.Price, b.BarsAgo, b.Price, BoxBrush, Brushes.Transparent, 10, true);
				rc.OutlineStroke.Width = 1;
				rc.OutlineStroke.DashStyleHelper = DashStyleHelper.Dot;
            }
        }
		#endregion

		#region Announce
		void Announce(AlertType key)
		{
			var soundfile = SoundAlerts ? AlertFilename(key) : "";
			if (TextAlerts)
				Alert(key.ToString(), Priority.High, AlertText(key), soundfile, 1, Brushes.White, BoxBrush);
		}
		#endregion

        #region Properties
		[XmlIgnore()]
		[Display(Name = "Color", GroupName = "Parameters", Order = 1, Description = "The color of the congestion box, alerts and chart markers.")]
		public Brush BoxBrush {
			get; set;
		}

		[Browsable(false)]
		public string BoxBrushSerialize {
			get { return Serialize.BrushToString(BoxBrush); }
			set { BoxBrush = Serialize.StringToBrush(value); }
		}
		
		[Display(Name = "Plot congestion boxes", Order = 2, GroupName = "Parameters", Description = "Enable or disable the plotting of congestion boxes")]
		[Browsable(true)]	// do not remove! this overrides base
		public override bool PlotBox {
			get { return base.PlotBox; }
			set { base.PlotBox = value; }
		}

		[Display(Name = "Bar Height Factor", Order = 3, GroupName = "Parameters", Description = "Congestion reference bars may not be taller than ATR(14) * this number")]
		[Browsable(true)]	// do not remove! this overrides base
		public override double BarHeightFactor
		{
			get; set;
		}
		
		[Display(Name = "Text alerts", Order = 4, GroupName = "Parameters", Description = "Show text alerts in the alerts window.")]
		public bool TextAlerts {
			get; set;
		}

		[Display(Name = "Sound alerts", Order = 5, GroupName = "Parameters", Description = "Play sound alerts for all events.")]
		public bool SoundAlerts {
			get; set;
		}

		[Display(Name = "Voice", Order = 6, GroupName = "Parameters", Description = "Choose the voice for the generated audio files")]
		[TypeConverter(typeof(TradeSafe3.VoiceConverter))]
		public string Voice {
			get; set;
		}

		[Display(Name = "Use last 60 bars only", Order = 7, GroupName = "Parameters", Description = "Reduce system load and increase speed by looking for congestion only in the last 60 bars")]
		public bool LastOnly {
			get; set;
		}

        #endregion
	}
}































































































































































