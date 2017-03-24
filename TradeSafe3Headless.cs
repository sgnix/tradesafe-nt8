#region Using declarations
using System;
using System.ComponentModel;
using System.Xml.Serialization;
using NinjaTrader.NinjaScript;
#endregion

namespace TradeSafe3
{
    using NinjaTrader.Data;
    using NinjaTrader.NinjaScript.Indicators;

    #region Defs
    public enum BreakoutMethod { None, Crossover, Swing, Both, Lost };
    public enum Direction { None, Up, Down };
    public enum State { None, Inside, Breakout, Reset, Fakeout, Confirmed, Canceled };
	#endregion

    #region Bar
    public class Bar
    {
        int idx;
        Bars bars;

        public Bar(Bars bars, int idx)
        {
            this.bars = bars;
            this.idx = idx;
        }

        #region OHLC
        public double Open
        {
            get { return bars.GetOpen(idx); }
        }

        public double High
        {
            get { return bars.GetHigh(idx); }
        }

        public double Low
        {
            get { return bars.GetLow(idx); }
        }

        public double Close
        {
            get { return bars.GetClose(idx); }
        }
        #endregion

        // Returs the size of the bar body
        public double Body
        {
            get { return Math.Abs(Open - Close); }
        }

        // Return the height for the entire candle, including the wicks
        public double Height
        {
            get { return High - Low; }
        }

        // Returns the highest point of the bar body
        public double Top
        {
            get { return Math.Max(Open, Close); }
        }

        // Returns the lowest point of the bar body
        public double Bottom
        {
            get { return Math.Min(Open, Close); }
        }

        // Checks if the bar traded higher than bar b
        public bool TradedHigherThan(Bar b)
        {
            return High > b.High;
        }

        // Checks if the bar traded lower than bar b
        public bool TradedLowerThan(Bar b)
        {
            return Low < b.Low;
        }

        // Checks if the bar's body is higher than the body of bar b
        public bool BodyHigher(Bar b)
        {
            return Top > b.Top;
        }

        // Checks if the bar's body is lower than the body of bar b
        public bool BodyLower(Bar b)
        {
            return Bottom < b.Bottom;
        }

		#region AreaOutside
        // Returns the value of the bar area that is ourside of a high/low range
        public double AreaOutside(double high, double low)
        {
            var top = Top - high;
            var bot = low - Bottom;
            double area = 0;

            if (top > 0)
                area += top;

            if (bot > 0)
                area += bot;

            return area;
        }
		#endregion

		#region ConfirmTrend
        // Checks if bar a confirms the trend of bar b, i.e. in an uptrend it
        // should trade higher than bar b and in a downtrend it should trade
        // lower than bar b.
        public bool ConfirmTrend(Direction dir, Bar b)
        {
            return dir == Direction.Up   && TradedHigherThan(b)
                || dir == Direction.Down && TradedLowerThan(b);
        }
		#endregion
    }
    #endregion

	#region Box
    public class Box
    {
        int minCongestionBars = 8;
        int barsAllowedOutside = 2;
        double areaAllowedOutside = 0.5;
        int barsToCancel = 6;

        int startBar;
        int endBar;
        double startY;
        double endY;

        State state = State.None;
        Direction dir = Direction.None;

        int refBarIdx = -1;
        int breakBarIdx = 0;
        int confirmBarIdx = 0;

        // User supplied function, computing the maximum height of the
        // congestion reference bar.  The function must take IDataSeries and
        // bar index and will return the maximum allowed bar height.
		Func<ISeries<double>, int, double> MaxBarHeightFunc;

		public Box(Func<ISeries<double>, int, double> mbh)
		{
			MaxBarHeightFunc = mbh;
		}

        // Check if bar i closed inside the box
        bool BarClosedInside(Bar bar)
        {
            return endY < bar.Close && bar.Close < startY;
        }

        // Checks if the bar broke out of the box and sets dir to the direction of the BO
        bool IsBreakout(Bar bar)
        {
            if (bar.Top > startY)
                dir = Direction.Up;
            else if (bar.Bottom < endY)
                dir = Direction.Down;
            else
                return false;

            return true;
        }

        // Checks if the bar broke out of the box in the opposite direction of dir and set dir in the new direction
        bool IsReverseBreakout(Bar bar)
        {
            if (dir == Direction.Down && bar.Top > startY)
                dir = Direction.Up;
            else if (dir == Direction.Up && bar.Bottom < endY)
                dir = Direction.Down;
            else
                return false;

            return true;
        }

        int HasCongestion(Bars bars, int sb, int eb)
        {
            int      i;
            int      len = eb - sb + 1;
            int[]    idx = new int[len];
            double[] hgt = new double[len];
			double   maxBarHeight;
            Bar      bar;
            bool     found = false;

            // Collect heights and indexes
            for (i = 0; i < len; i++)
            {
                bar = new Bar(bars, sb + i);
                hgt[i] = bar.Height;
                idx[i] = sb + i;
            }

            // Sort, using heights as key and indexes as values
            Array.Sort(hgt, idx);

            // Begin searching for the reference bar at the 3rd highest bar
            i = len - 4;
            do
            {
                i++;
                bar = new Bar(bars, idx[i]);

				// Compute the average height of the bar using the user provided computing function.
				// Note that the indexing is in "barsAgo" style, not absolute.
				maxBarHeight = MaxBarHeightFunc(bars, bars.CurrentBar - idx[i]);

				// Only entertain bars that are less than X taller than
				// the average height. Otherwise we may get a single very tall bar
				// that creates a very tall congestion, making it difficult to
				// break out of that congestion.
				if (bar.Height <= maxBarHeight)
                	found = IsCongestion(bars, sb, eb, bar.High, bar.Low);
            } while ( !found && i < len - 1);

            return found ? idx[i] : -1;
        }

        // Returns if the rectangle is a congestion according to Tradesafe's rules
        bool IsCongestion(Bars bars, int sb, int eb, double sy, double ey)
        {
            var i = sb;

            // Number of bars "slightly" outside
            int barsOutside = 0;

            while (i <= eb)
            {
                var bar = new Bar(bars, i);
                var area = bar.AreaOutside(sy, ey);
                if (area > 0)
                {
                    // Bar too big?
                    if (area / bar.Body > areaAllowedOutside)
                        return false;

                    // Too many bars outside?
                    if (++barsOutside > barsAllowedOutside)
                        return false;
                }
                i++;
            }

            return true;
        }

        // Identifies a new congestion
        public bool Find(Bars bars, int lastBar)
        {
            if (lastBar < minCongestionBars)
                return false;

            int sb, eb, rIdx;
            double sy, ey;
            Bar rBar;
            bool result = false;

            sb = lastBar - minCongestionBars;
            eb = lastBar;

            rIdx = HasCongestion(bars, sb, eb);
            if (rIdx != -1 && rIdx != refBarIdx)
            {
                rBar = new Bar(bars, rIdx);

                sy = rBar.High;
                ey = rBar.Low;

                while (sb >= 0 && eb - sb <= minCongestionBars && IsCongestion(bars, sb, eb, sy, ey))
                {
                    refBarIdx = rIdx;
                    startBar = sb;
                    endBar = eb;
                    startY = sy;
                    endY = ey;
                    result = true;

                    sb--;
                }
            }

            return result;
        }

        // Determines if a breakout identified by the breakBarIdx bar is
        // confirmed by the bar with index idx. This method is public, because
        // it's used in OnBarUpdate as well as in Recalc.
        public bool ConfirmBreakout(Bars bars, int idx)
        {
            if ( state != State.Breakout )
                return false;

            var a = new Bar(bars, idx);
            var b = new Bar(bars, breakBarIdx);

            if (a.ConfirmTrend(dir, b))
            {
                state = State.Confirmed;
                confirmBarIdx = idx;
                return true;
            }

            return false;
        }

        // Recalculate the state of the box, starting from bar idx
        public void Recalc(Bars bars, int idx, int lastBar)
        {
            int i = idx;

            if (state == State.None) return;

            while (i <= Math.Max(lastBar, endBar))
            {
                var bar = new Bar(bars, i);

                switch (state)
                {
                    case State.Inside:
                    case State.Reset:
                    case State.Fakeout:

                        dir = Direction.None;
                        state = State.Inside;
                        endBar = i;

                        // Look for a breakout
                        if (IsBreakout(bar))
                        {
                            state = State.Breakout;
                            breakBarIdx = i;
                        }

                        break;

                    case State.Breakout:
                        if (BarClosedInside(bar))
                        {
                            // Breakout reset
                            state = State.Reset;
                            dir = Direction.None;
                            endBar = i;
                        }
                        else if (ConfirmBreakout(bars, i))
                        {
                            // Breakout confirmend
                            // ConfirmBreakout updates the state of the box
                        }
                        else if (IsReverseBreakout(bar))
                        {
                            // It's a breakout in the opposite direction
                            state = State.Breakout;
                            breakBarIdx = i;
                            endBar = i;
                        }

                        break;

                    case State.Confirmed:
                        if (BarClosedInside(bar))
                        {
                            // The bar closed inside the box. Breakout fakeout!
                            state = State.Fakeout;
                            dir = Direction.None;
                            endBar = i;
                        }
                        else if (IsReverseBreakout(bar))
                        {
                            // It's a breakout in the opposite direction
                            state = State.Breakout;
                            breakBarIdx = i;
                            endBar = i;
                        }
                        else
                        {
                            // The bar closed outside of the box. We are still in a breakout.
                            // Now begin counting how many bars will keep outside.
                            if (i - confirmBarIdx >= barsToCancel)
                            {
                                state = State.Canceled;
                                dir = Direction.None;
                            }
                        }

                        break;

                    case State.Canceled:
                        state = State.None;
                        break;
                }

                i++;
            }
        }

        public void Process(Bars bars, int lastBar)
        {
            if (Find(bars, lastBar))
            {
                // If a new or better box is found, then we reset everything and
                // recalculate its state from the begining.
                state = State.Inside;
                dir = Direction.None;
                Recalc(bars, startBar, lastBar);

                // A new recalculation of the entire box may determine that the reference
                // bar, which may also be the last bar is a breakout reset, so we manually
                // forse the state back to Inside.
                if (refBarIdx == lastBar)
                    state = State.Inside;
            }
            else
                // Otherwise we calculate the last bar
                Recalc(bars, lastBar, lastBar);
        }

        public State State
        {
            get { return state; }
        }

        public Direction Dir
        {
            get { return dir; }
        }

        public int StartBar
        {
            get { return startBar; }
        }

        public int EndBar
        {
            get { return endBar; }
        }

        public double StartY
        {
            get { return startY; }
        }

        public double EndY
        {
            get { return endY; }
        }

        public int ReferenceBar
        {
            get { return refBarIdx; }
        }

        public int BreakoutBar
        {
            get { return breakBarIdx; }
        }

        public int ConfirmationBar
        {
            get { return confirmBarIdx; }
        }
    }
	#endregion

	#region Breakout Classes
	#region Breakout
    class Breakout
    {

        #region Vars
        protected Indicator parent;
        protected Direction dir = Direction.None;
        protected int[] bars;				// 0 - breakout bar, 1 ... - confirmation bars
        protected int idx;			        // current bar being tested
        protected int size;				    // count of bars array
        #endregion

        public Breakout(Indicator parent, int size)
        {
            this.parent = parent;
            this.size = size;
            bars = new int[size];
            idx = 0;
        }

        // Returns true if we can begin looking for a breakout.
        // Ideally this checks if we have enough bars to continue calculations.
        protected virtual bool CanProcess()
        {
            return true;
        }

        // Returns direction at the first signal of change
        protected virtual Direction Detect()
        {
            return Direction.None;
        }

        // Returs true if the current bar confirms the trend of bar with index i
        protected virtual bool Confirm(int i)
        {
            return false;
        }

        // Returns true if there is a confirmed breakout
        public bool Find()
        {
            if (!CanProcess())
                return false;

            var d = Detect();

            if (d != Direction.None)
            {
                idx = 0;
                dir = d;
                bars[idx] = LastBar;
                idx++;
            }

            if (idx > 0)
            {
                if (Confirm(bars[idx - 1]))
                {
                    bars[idx] = LastBar;  // XXX: Inaccurate if using CurrentBar as in Swing method
                    if (idx < size - 1)
                    {
                        idx++;
                    }
                    else
                    {
                        idx = 0;
                        return true;
                    }
                }
            }

            return false;
        }

		public void Reset()
		{
			idx = 0;
		}

        public Direction Dir
        {
            get { return dir; }
        }

		protected int LastBar
		{
			get { return parent.State == NinjaTrader.NinjaScript.State.Historical ? parent.CurrentBar : parent.CurrentBar - 1; }
		}
    }
	#endregion

	#region CrossOverBreakout
    class CrossOverBreakout : Breakout
    {
        const int slowMADays = 13;
        const int fastMADays = 7;

        public CrossOverBreakout(Indicator parent)
            : base(parent, 3)
        {
        }

        // Can only begin processing after there are enough bars to compute the slow EMA
        protected override bool CanProcess()
        {
            return LastBar >= slowMADays;
        }

        // Initial breakout signal is issued when the fast EMA crosses the slow EMA.
        // The CrossOver method provided by NT triggers after the touch, so we need
        // to build our own touch detection.
        protected override Direction Detect()
        {
			if (parent.IsFirstTickOfBar)
			{
				var f = parent.EMA(fastMADays);
				var s = parent.EMA(slowMADays);
				var i = parent.State == NinjaTrader.NinjaScript.State.Historical ? 0 : 1;

				if ((f[i] == s[i] || f[i] > s[i]) && (f[i + 1] < s[i + 1]))
				{
					// If the bar on the crossover is higher than the MA, then
					// we only need one more to confirm the breakout. We hack
					// the array size to one less element.
					//if (parent.Close[i] > s[i])
					//	size = 2;
					// XXX: disabled - possibly inaccurate

					return Direction.Up;
				}

				if ((f[i] == s[i] || f[i] < s[i]) && (f[i + 1] > s[i + 1]))
				{
					// If the bar on the crossover is higher than the MA, then
					// we only need one more to confirm the breakout.
					//if (parent.Close[i] < s[i])
					//	size = 2;
					// XXX: disabled - possibly inaccurate

					return Direction.Down;
				}
			}

            return base.Detect();
        }

        // We need 2 bars confirmation after the initial signal. The first one must close above
        // the EMA, and the second one must close above the first one.
        // From the TradeSafe manual:
        // "To increase your odds of having a viable trend after a crossover is made, wait for at
        // least two higher/lower closes beyond the crossover point."
        protected override bool Confirm(int i)
        {
			if (!parent.IsFirstTickOfBar || LastBar == i) return false;

            var a = new Bar(parent.Bars, LastBar);
            var b = new Bar(parent.Bars, i);
            return (dir == Direction.Up   && a.BodyHigher(b)
                 || dir == Direction.Down && a.BodyLower(b));
        }
    }
	#endregion

	#region SwingBreakout
    class SwingBreakout : Breakout
    {
        const int swingDays = 5;

        public SwingBreakout(Indicator parent)
            : base(parent, 2)
        {
        }

        protected override bool CanProcess()
        {
            return parent.CurrentBar >= swingDays;
        }

		// The breakout bar must close beyond the swing high/low. This is why
		// it is calculated at close.
        protected override Direction Detect()
        {
			if (parent.IsFirstTickOfBar)
			{
				var last = new Bar(parent.Bars, LastBar - 1);
				var curr = new Bar(parent.Bars, LastBar);
				var h = parent.Swing(swingDays).SwingHigh[0];
				var l = parent.Swing(swingDays).SwingLow[0];

				if (last.Top <= h && curr.Top > h)
					return Direction.Up;

				if (last.Bottom >= l && curr.Bottom < l)
					return Direction.Down;
			}

            return base.Detect();
        }

		// The confirmation bar is calculated in real time, because it's enough that it trades
		// above the breakout bar.
		// "In an uptrend, the moment price turns down and closes below a prior Swing Low (call
		// this the breakout bar), followed by a trade below the low of the breakout bar, and vice
		// versa when in a downtrend"
        protected override bool Confirm(int j)
        {
			var a = new Bar(parent.Bars, parent.CurrentBar);
			var b = new Bar(parent.Bars, j);
			return a.ConfirmTrend(dir, b);
        }
    }
	#endregion
	#endregion
}

namespace NinjaTrader.NinjaScript.Indicators
{
    public class TradeSafe3Headless : Indicator
	{
        #region Constants
        const int    atrAvg = 14;               // ATR days average
        const double barHeightFactor = 2;       // Reference bar may not be taller than ATR * this
        #endregion

		#region Variables

		// Trend
		TradeSafe3.BreakoutMethod  method = TradeSafe3.BreakoutMethod.None;

		// Detect congestion
		TradeSafe3.Box box;
		bool plotBox = true;

		// Index of the last reference bar, confirmation bar and breakout bar
		int lastRefIdx      = -1;
		int lastConfirmIdx  = -1;
		int lastBreakoutIdx = -1;
		int lastReversalIdx = -1;

		// Detect trend
		TradeSafe3.CrossOverBreakout 	crossover;
		TradeSafe3.SwingBreakout 		swing;

		// Data series
		Series<TradeSafe3.Direction> trendValues;
		#endregion

        #region OnStateChange
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Combined trend indicator that does not plot any drawings.";
				Name										= "TradeSafe3Headless";
				Calculate									= Calculate.OnPriceChange;
				IsOverlay									= false;
				DisplayInDataBox							= false;
				DrawOnPricePanel							= false;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= false;
				PaintPriceMarkers							= false;
				IsSuspendedWhileInactive					= true;
			}
			else if (State == State.Configure)
			{
                crossover = new TradeSafe3.CrossOverBreakout(this);
                swing     = new TradeSafe3.SwingBreakout(this);
                trendValues = new Series<TradeSafe3.Direction>(this);

                // Create function to be used to compute the average height of a bar.
                // It passed into the Box constructor and used by the Box class to skip
                // single bars that are too long, that create an unreasonably high congestion.
                // Currently the Average True Range indicator is used to compute the height.
                Func<ISeries<double>, int, double> r = delegate (ISeries<double> bars, int idxAgo)
                {
                    return ATR(bars, atrAvg)[idxAgo] * barHeightFactor;
                };
                box = new TradeSafe3.Box(r);


                //VendorLicense("TradeSafe", "TradeSafe2Trend", "http://daytradesafe.com", "mguess@daytradesafe.com");
			}
		}
        #endregion

		#region OnBarUpdate
        protected override void OnBarUpdate()
        {
			if (CurrentBar < atrAvg) return;

			if (plotBox)
			{
				// If we're processing historical data, then we process all bars up
				// to CurrentBar including it, because we know all bars have
				// completed.  If we're processing realtime data, then we go up to
				// the previous bar, because the CurrentBar is still ticking.
				box.Process(Bars, State == State.Historical ? CurrentBar : CurrentBar - 1);

				// If we're processing a live ticking bar, then we'll independently
				// test for breakout confirmation. This is the only time we make a
				// decision before the bar has closed. ConfirmBreakout, will check
				// for the necessary state and update the internals of the box.
				box.ConfirmBreakout(Bars, CurrentBar);

				if (box.State == TradeSafe3.State.Confirmed)
				{
					Trend[0] = box.Dir;

					// Save the confirmation bar to prevent triggering OnBreakoutConfirmed
					// on the bars between the confirmation and the cancelation
					if (box.ConfirmationBar != lastConfirmIdx)
					{
						lastConfirmIdx = box.ConfirmationBar;
						OnBreakoutConfirmed();
					}
				}
			}

			// If we're trading outside of any kind of boxes, then we begin
            // looking for a trend reversal
			if (box.State == TradeSafe3.State.None && TrendChanged())
				OnTrendChange();

			// Everything below the follwoing line will be run only once per bar, and it
			// will apply for the previous bar, which fully closed
			if (!IsFirstTickOfBar) return;

			switch(box.State)
			{
                // When we get inside the box, whether it's the first time or
                // one of the resets or fakeouts, we must reset the swing and
                // crossover indicator, in case they've detected the breakout
                // bar and are waiting for the confirmation.
				case TradeSafe3.State.Inside:
				case TradeSafe3.State.Reset:
				case TradeSafe3.State.Fakeout:
					swing.Reset();
					crossover.Reset();
					Trend[0] = TradeSafe3.Direction.None;

					if (box.State == TradeSafe3.State.Inside)
					{
						if (box.ReferenceBar != lastRefIdx)
						{
							lastRefIdx = box.ReferenceBar;
							OnNewCongestion();
						}
					}
					else if (box.State == TradeSafe3.State.Reset)
						OnBreakoutReset();
					else if (box.State == TradeSafe3.State.Fakeout)
						OnBreakoutFakeout();

					break;

				case TradeSafe3.State.Breakout:
					Trend[0] = TradeSafe3.Direction.None;

					// Save the last breakout bar to prevent triggering OnBreakout
					// on the bars between the breakout and the confirmation
					if (box.BreakoutBar != lastBreakoutIdx)
					{
						lastBreakoutIdx = box.BreakoutBar;
						OnBreakout();
					}
					break;

				case TradeSafe3.State.Canceled:
					Trend[0] = Trend[1];
					OnCongestionCanceled();
					break;
			}
        }
		#endregion

		#region Virtual
		protected virtual void OnTrendChange()
		{
		}

		protected virtual void OnNewCongestion()
		{
		}

		protected virtual void OnBreakout()
		{
		}

		protected virtual void OnBreakoutReset()
		{
		}

		protected virtual void OnBreakoutFakeout()
		{
		}

		protected virtual void OnBreakoutConfirmed()
		{
		}

		protected virtual void OnCongestionCanceled()
		{
		}
		#endregion

		#region TrendChanged
        // Looks for a change of the trend using the swing and crossover methods.
        // Returns true, if the trend changed and false otherwise.
		bool TrendChanged() {
			bool co, sw, ls = false;
			var lastTrend = CurrentBar > 0 ? Trend[1] : TradeSafe3.Direction.None;
			var newTrend = lastTrend;

            // If we detect trend change (reversal), we save the id of the bar,
            // so we can skip the next iteration.  Remember, this function runs
            // in real time, i.e. on each tick, i.e. several times per bar.
			if (CurrentBar == lastReversalIdx)
				return false;

            // Look for a confirmed crossover reveral
			if ( co = crossover.Find() ) {
				newTrend = crossover.Dir;
				method = TradeSafe3.BreakoutMethod.Crossover;
			}

            // Look for a confirmed swings reversal
			if ( sw = swing.Find() ) {
				newTrend = swing.Dir;
				method = TradeSafe3.BreakoutMethod.Swing;
			}

            // If both at the same bar, then mark it as such.  Note: This will
            // *never* happen in real time, because the swings indicator is
            // always bound to kick in first, because its confirmation is a
            // _trade_ above the breakout bar, while the crossover confirmation
            // is a _close_ above the breakout bar.  A black bar is only going
            // to show up when rendering historical data
			if ( co && sw )
				method = TradeSafe3.BreakoutMethod.Both;

            // Check for loss of trend. We lose the trend when a bar trades
            // close to the opposite swing, for example if a bar trandes close
            // to the swing high, while we're in a downtrend, this should case
            // the loss of trend.  This is only triggered if no other trend
            // reversals are detected.
			if ( !co && !sw)
			{
				ls = lastTrend == TradeSafe3.Direction.Down && Math.Max(High[0], Low[0]) >= Swing(5).SwingHigh[0]
				  || lastTrend == TradeSafe3.Direction.Up   && Math.Min(High[0], Low[0]) <= Swing(5).SwingLow[0];

				if ( ls )
				{
					newTrend = TradeSafe3.Direction.None;
					method = TradeSafe3.BreakoutMethod.Lost;
				}
			}

            // Set the trend data series
			Trend[0] = newTrend;

            // If either of the trend reversal variables turned out true, then
            // we save the reversal bar and return true, notifying the caller
            // that we've found a trend reversal
			if ((co || sw || ls) && newTrend != lastTrend)
			{
				lastReversalIdx = CurrentBar;
				return true;
			}

			return false;
		}
		#endregion

        #region Properties
        [Browsable(false)]
        [XmlIgnore()]
        public TradeSafe3.BreakoutMethod Method
        {
            get { return method; }
        }

		[Browsable(false)]
        [XmlIgnore()]
        public TradeSafe3.Box Box
        {
            get { return box; }
        }

		[Browsable(false)]
        [XmlIgnore()]
        public Series<TradeSafe3.Direction> Trend
        {
            get { return trendValues; }
        }

		[Browsable(false)]
        [XmlIgnore()]
        public virtual bool PlotBox
        {
            get { return plotBox; }
			set { plotBox = value; }
        }
		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private TradeSafe3Headless[] cacheTradeSafe3Headless;
		public TradeSafe3Headless TradeSafe3Headless()
		{
			return TradeSafe3Headless(Input);
		}

		public TradeSafe3Headless TradeSafe3Headless(ISeries<double> input)
		{
			if (cacheTradeSafe3Headless != null)
				for (int idx = 0; idx < cacheTradeSafe3Headless.Length; idx++)
					if (cacheTradeSafe3Headless[idx] != null &&  cacheTradeSafe3Headless[idx].EqualsInput(input))
						return cacheTradeSafe3Headless[idx];
			return CacheIndicator<TradeSafe3Headless>(new TradeSafe3Headless(), input, ref cacheTradeSafe3Headless);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.TradeSafe3Headless TradeSafe3Headless()
		{
			return indicator.TradeSafe3Headless(Input);
		}

		public Indicators.TradeSafe3Headless TradeSafe3Headless(ISeries<double> input )
		{
			return indicator.TradeSafe3Headless(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.TradeSafe3Headless TradeSafe3Headless()
		{
			return indicator.TradeSafe3Headless(Input);
		}

		public Indicators.TradeSafe3Headless TradeSafe3Headless(ISeries<double> input )
		{
			return indicator.TradeSafe3Headless(input);
		}
	}
}

#endregion
