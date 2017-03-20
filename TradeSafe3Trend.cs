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

//This namespace holds Indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators
{
    public class TradeSafe3Trend : TradeSafe3Headless
	{
		enum AlertType { None, Reversal, TrendLost, Congestion, Breakout, Reset, Fakeout, Confirmed, Canceled};

        #region Constants
		// Tagnames
		const string boxTag   = "@congestion";
		const string refTag   = "@refbar";
		const string arrowTag = "@breakout-arrow";

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
		SpeechSynthesizer synth;
		string voice;
		Dictionary<AlertType, string> alerts;

		// Currently drawn box
		Rectangle ir;

		// Parameters
		Brush   boxBrush 	= Brushes.Blue;
		Brush   shadowBrush = Brushes.Gray;
		bool 	textAlerts 	= true;
		bool    soundAlerts = true;
		bool	lastOnly    = true;
        #endregion

		#region OnStateChange
		protected override void OnStateChange()
		{
            base.OnStateChange();

			if (State == State.SetDefaults)
			{
				Description	= @"An implementation of Michael Guess' TradeSafe trend change indicator.";
				Name	   	= "TradeSafe_Trend";
			}
			else if (State == State.Configure)
			{
                synth = new SpeechSynthesizer();
                voice = synth.Voice.Name;
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
			if (lastOnly && Bars.Count - CurrentBar > maxLastBar)
				return;

			try {
				base.OnBarUpdate();
			}
			catch (Exception e) {
				Print(e.ToString());
			}

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

			synth.SelectVoice(voice);

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
			DrawShadow();
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

			// Shadow
			DrawShadow();

			RemoveDrawObject(refTag);
			RemoveDrawObject(arrowTag);
			RemoveDrawObject(boxTag);
		}
		#endregion
		
		#region DrawMainBox
		void DrawMainBox()
		{
			int r = CurrentBar - Box.ReferenceBar;
			Draw.Diamond(this, refTag, false, r, High[r] + TickSize, boxBrush, true);
			ir = Draw.Rectangle(this, boxTag, true, CurrentBar - Box.StartBar, Box.StartY, CurrentBar - Box.EndBar, Box.EndY, Brushes.Transparent, boxBrush, 20, true);
		}
		#endregion

		#region DrawShadow
		void DrawShadow()
		{
			if ( ShadowBrush != Brushes.Transparent && ir != null )
			{
				var tag = "shadow_" + Time[0].ToString("HH:mm:ss");
				var shadow = Draw.Rectangle(this, tag, true, ir.StartAnchor.BarsAgo, ir.StartAnchor.Price, ir.EndAnchor.BarsAgo, ir.EndAnchor.Price, Brushes.Transparent, ShadowBrush, 20, true);
			}
		}
		#endregion

		#region Announce
		void Announce(AlertType key)
		{
			var soundfile = soundAlerts ? AlertFilename(key) : "";
			if (textAlerts)
				Alert(key.ToString(), Priority.High, AlertText(key), soundfile, 1, Brushes.White, boxBrush);
		}
		#endregion

        #region Properties
		[XmlIgnore()]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Current congestion box", GroupName = "Colors")]
		[Description("The color of the congestion box, alerts and chart markers.")]
		public Brush BoxBrush {
			get { return boxBrush; }
			set { boxBrush = value; }
		}

		[Browsable(false)]
		public string BoxBrushSerialize {
			get { return Serialize.BrushToString(boxBrush); }
			set { boxBrush = Serialize.StringToBrush(value); }
		}

		[Display(ResourceType = typeof(Custom.Resource), Name = "Expired congestion box", GroupName = "Colors")]
		[Description("The color of the congestion box after it's no longer active.")]
		public Brush ShadowBrush {
			get { return shadowBrush; }
			set { shadowBrush = value; }
		}

		[Browsable(false)]
		public string ShadowBrushSerialize {
			get { return Serialize.BrushToString(shadowBrush); }
			set { shadowBrush = Serialize.StringToBrush(value); }
		}

		[Display(ResourceType = typeof(Custom.Resource), Name = "Text alerts", GroupName = "Alerts")]
		[Description("Post text alerts in the alerts window.")]
		public bool TextAlerts {
			get { return textAlerts; }
			set { textAlerts = value; }
		}

		[Display(ResourceType = typeof(Custom.Resource), Name = "Sound alerts", GroupName = "Alerts")]
		[Description("Play sound alerts for all events.")]
		public bool SoundAlerts {
			get { return soundAlerts; }
			set { soundAlerts = value; }
		}

		[Display(ResourceType = typeof(Custom.Resource), Name = "Voice", GroupName = "Alerts")]
		[Description("Choose the voice for the generated audio files")]
		[TypeConverter(typeof(VoiceConverter))]
		public string Voice {
			get { return voice; }
			set { voice = value; }
		}

		[Display(ResourceType = typeof(Custom.Resource), Name = "Use last 60 bars only", GroupName = "Parameters")]
		[Description("Reduce system load and increase speed by looking for congestion only in the last 60 bars")]
		public bool LastOnly {
			get { return lastOnly; }
			set { lastOnly = value; }
		}

		[Display(ResourceType = typeof(Custom.Resource), Name = "Plot congestion boxes", GroupName = "Parameters")]
		[Description("Enable or disable the plotting of congestion boxes")]
		[Browsable(true)]
		public override bool PlotBox {
			get { return base.PlotBox; }
			set { base.PlotBox = value; }
		}

        #endregion
	}
}




































