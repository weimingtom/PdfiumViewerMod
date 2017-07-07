using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using PdfiumViewer;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Windows.Input;

namespace PdfiumViewer
{
	/// <summary>
	/// Description of CustomScrollControl.
	/// </summary>
	public class CustomScrollControl : FrameworkElement, IScrollInfo
	{
		protected long lastElapsed = 0;
        protected int lastTick = Environment.TickCount;
		
		private const int scrollingTime = 500;
		private bool _isScrolling;
		public bool isScrolling 
		{ 
			get 
			{ 
				return _isScrolling;
			}
			set 
			{
				/*
				if (value == true)
				{
					Debug.WriteLine("isScrolling == true");
				}
				else
				{
					Debug.WriteLine("isScrolling == false");
				}*/
				_isScrolling = value;
			}
		}
		private int lastInvidateTime = 0;		
		private DispatcherTimer timer = new DispatcherTimer();
        
		public CustomScrollControl()
		{
			//this.LayoutUpdated += delegate(object sender, EventArgs e) { this.OnLayout(); };
//			this.SizeChanged += delegate(object sender, SizeChangedEventArgs e) { 
//				this.OnLayout();
//			};
			this.Loaded += delegate(object sender, RoutedEventArgs e) { OnLoad(); };
		}
		
        protected void fastDraw()
        {
        	this.isScrolling = true;
        	this.lastInvidateTime = Environment.TickCount;
        }		
		
		private void OnLoad()
		{
			timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += timer_Tick;
            timer.Start();
            this.onTick();   
		}
		
        void timer_Tick(object sender, EventArgs e)
        {
            this.onTick();
        }
		
        private void onTick()
        {
        	int delta = Environment.TickCount - this.lastInvidateTime;
        	//FIXME:大于某数值，使得在停止滚动之后不久设置为false；小于某数值，防止不断循环刷屏幕
        	if (delta > scrollingTime && delta < scrollingTime + timer.Interval.Milliseconds * 2) 
        	{
        		isScrolling = false;
        		Invalidate();
        	}
        }		
		
		protected virtual void OnLayout()
		{
			//do nothing
		}

		protected virtual void OnPaint(DrawingContext drawingContext)
		{
			//do nothing
		}
		
        //改变窗口大小事件
		protected override Size MeasureOverride(Size availableSize)
		{
			//Debug.WriteLine("MeasureOverride : " + availableSize);
            this.OnLayout();
            //这里计算内容大小（不受界面影响），内容大小>=窗口大小
			Size sz = new Size(
				Math.Max(availableSize.Width, this.scrollExtent.Width),//this.ContentWidth), 
                Math.Max(availableSize.Height, this.scrollExtent.Height) //this.ContentHeight)
			);

			SetScrollData(availableSize, sz, scrollOffset);
			if (availableSize.Width == double.PositiveInfinity)
				availableSize.Width = SystemParameters.PrimaryScreenWidth;
			if (availableSize.Height == double.PositiveInfinity)
				availableSize.Height = SystemParameters.PrimaryScreenHeight;			
			return availableSize;
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
            //CalculateLineLayout();
            OnPaint(drawingContext);
		}

		public static readonly DependencyProperty ColumnWidthProperty =
			PdfArea.ColumnWidthProperty.AddOwner(typeof(PdfRenderer));

		protected Size scrollExtent; //内容大小 （大于窗口大小时显示滚动条）

		protected Vector scrollOffset; //偏移（通过IScrollInfo回调同步）

        protected Size scrollViewport; //窗口大小（通过MeasureOverride回调同步）

		internal void ClearScrollData()
		{
			SetScrollData(new Size(), new Size(), new Vector());
		}

		bool SetScrollData(Size viewport, Size extent, Vector offset)
		{
			if (!(IsClose(viewport, this.scrollViewport)
			      && IsClose(extent, this.scrollExtent)
			      && IsClose(offset, this.scrollOffset)))
			{
				this.scrollViewport = viewport;
				this.scrollExtent = extent;
				SetScrollOffset(offset);
				this.OnScrollChange();
				return true;
			}
			return false;
		}

		void OnScrollChange()
		{
			ScrollViewer scrollOwner = ((IScrollInfo)this).ScrollOwner;
			if (scrollOwner != null)
			{
				scrollOwner.InvalidateScrollInfo();
			}
		}

		bool canVerticallyScroll;
		bool IScrollInfo.CanVerticallyScroll
		{
			get { 
				Debug.WriteLine("IScrollInfo.CanVerticallyScroll = " + canVerticallyScroll);
				return canVerticallyScroll;
			}
			set
			{
				if (canVerticallyScroll != value)
				{
					canVerticallyScroll = value;
					InvalidateMeasure(DispatcherPriority.Normal);
				}
			}
		}
		bool canHorizontallyScroll;
		bool IScrollInfo.CanHorizontallyScroll
		{
			get { return canHorizontallyScroll; }
			set
			{
				if (canHorizontallyScroll != value)
				{
					canHorizontallyScroll = value;
					InvalidateMeasure(DispatcherPriority.Normal);
				}
			}
		}

		public double ExtentWidth
		{
			get
			{
				return scrollExtent.Width;
			}
		}

		public double ExtentHeight
		{
			get
			{
				return scrollExtent.Height;
			}
		}

		public double ViewportWidth
		{
			get
			{
				return scrollViewport.Width;
			}
		}

		public double ViewportHeight
		{
			get
			{
				return scrollViewport.Height;
			}
		}

		public double HorizontalOffset
		{
			get
			{
				return scrollOffset.X;
			}
		}

		public double VerticalOffset
		{
			get
			{
				return scrollOffset.Y;
			}
		}
		ScrollViewer _scrollOwner;
		public ScrollViewer ScrollOwner
		{
			get
			{
				return _scrollOwner;
			}
			set
			{
				_scrollOwner = value;
			}
		}

        private double scrollLineHeight = 50; //上下滚动的最小值 FIXME:
        private double scrollColWidth = 50; //左右滚动的最小值


		public void LineUp()
		{
            (this as IScrollInfo).SetVerticalOffset(scrollOffset.Y - this.scrollLineHeight);
		}

		public void LineDown()
		{
            (this as IScrollInfo).SetVerticalOffset(scrollOffset.Y + this.scrollLineHeight);
		}

		public void LineLeft()
		{
            (this as IScrollInfo).SetHorizontalOffset(scrollOffset.X - this.scrollColWidth);
		}

		public void LineRight()
		{
            (this as IScrollInfo).SetHorizontalOffset(scrollOffset.X + this.scrollColWidth);
		}

		public void PageUp()
		{
			(this as IScrollInfo).SetVerticalOffset(
				scrollOffset.Y + this.scrollLineHeight - this.scrollViewport.Height
			);
		}

		public void PageDown()
		{
			(this as IScrollInfo).SetVerticalOffset(
				scrollOffset.Y - this.scrollLineHeight + this.scrollViewport.Height
			);
		}

		public void PageLeft()
		{
			(this as IScrollInfo).SetHorizontalOffset(0.0d);
		}

		public void PageRight()
		{
			(this as IScrollInfo).SetHorizontalOffset(double.MaxValue);
		}

		void IScrollInfo.MouseWheelUp()
		{
			((IScrollInfo)this).SetVerticalOffset(
                scrollOffset.Y - (SystemParameters.WheelScrollLines * scrollLineHeight));
			OnScrollChange();
		}

		void IScrollInfo.MouseWheelDown()
		{
			((IScrollInfo)this).SetVerticalOffset(
                scrollOffset.Y + (SystemParameters.WheelScrollLines * scrollLineHeight));
			OnScrollChange();
		}

		void IScrollInfo.MouseWheelLeft()
		{
			//((IScrollInfo)this).SetHorizontalOffset(
			//    scrollOffset.X - (SystemParameters.WheelScrollLines * wideSpaceWidth));
			((IScrollInfo)this).SetHorizontalOffset(
                scrollOffset.X - (SystemParameters.WheelScrollLines * scrollColWidth));
			OnScrollChange();
		}

		void IScrollInfo.MouseWheelRight()
		{
			//((IScrollInfo)this).SetHorizontalOffset(
			//    scrollOffset.X + (SystemParameters.WheelScrollLines * wideSpaceWidth));
			((IScrollInfo)this).SetHorizontalOffset(
                scrollOffset.X + (SystemParameters.WheelScrollLines * scrollColWidth));
			OnScrollChange();
		}

		/// <summary>
		/// Occurs when the scroll offset has changed.
		/// </summary>
		public event EventHandler ScrollOffsetChanged;

		internal void SetScrollOffset(Vector vector)
		{
			if (!canHorizontallyScroll)
				vector.X = 0;
			if (!canVerticallyScroll)
				vector.Y = 0;

			vector.X = Math.Min(
				vector.X,
				scrollExtent.Width - scrollViewport.Width
			);
			
			double defaultLineHeight = this.scrollLineHeight;
			
			double t = Math.Round
				(
					(scrollExtent.Height - scrollViewport.Height) / defaultLineHeight,
					0
				);

			vector.Y = Math.Min(vector.Y, t * defaultLineHeight);

			if (!IsClose(scrollOffset, vector))
			{
				scrollOffset = vector;
				this.fastDraw();
				//this.InvalidateVisual();
				this.Invalidate();
				if (ScrollOffsetChanged != null)
					ScrollOffsetChanged(this, EventArgs.Empty);
			}
		}

		static double ValidateVisualOffset(double offset)
		{
			if (double.IsNaN(offset))
				throw new ArgumentException("offset must not be NaN");
			if (offset < 0)
				return 0;
			else
				return offset;
		}

		void IScrollInfo.SetHorizontalOffset(double offset)
		{
			offset = ValidateVisualOffset(offset);
			if (!IsClose(scrollOffset.X, offset))
			{
				SetScrollOffset(new Vector(offset, scrollOffset.Y));
				this.fastDraw();
				//InvalidateVisual(); //调用onRender
				this.Invalidate();
			}
		}

		void IScrollInfo.SetVerticalOffset(double offset)
		{
			offset = ValidateVisualOffset(offset);
			if (!IsClose(scrollOffset.Y, offset))
			{
				double defaultLineHeight = this.scrollLineHeight;
				offset = Math.Round((offset / defaultLineHeight), 0) * defaultLineHeight;
				SetScrollOffset(new Vector(scrollOffset.X, offset));
				InvalidateMeasure(DispatcherPriority.Normal);
				this.fastDraw();
				//InvalidateVisual();
				this.Invalidate();
			}
		}

		public Rect MakeVisible(Visual visual, Rect rectangle)
		{
			if (rectangle.IsEmpty || visual == null || visual == this || !this.IsAncestorOf(visual))
			{
				return Rect.Empty;
			}
			return rectangle;
		}

        /*
		double contentWidth;

		public double ContentWidth
		{
			get
			{
				CalculateLineLayout();
				return contentWidth;
			}
		}*/

        //计算内容大小
		void CalculateLineLayout()
		{
            /*
            contentWidth = 1000;
            contentHeight = 1000; //FIXME:???*/	
		}

		DispatcherOperation invalidateMeasureOperation;

		protected void InvalidateMeasure(DispatcherPriority priority)
		{
			if (priority >= DispatcherPriority.Render)
			{
				if (invalidateMeasureOperation != null)
				{
					invalidateMeasureOperation.Abort();
					invalidateMeasureOperation = null;
				}
				base.InvalidateMeasure();
				base.InvalidateVisual();
			}
			else
			{
				if (invalidateMeasureOperation != null)
				{
					invalidateMeasureOperation.Priority = priority;
				}
				else
				{
					invalidateMeasureOperation = Dispatcher.BeginInvoke(
						priority,
						new Action(
							delegate
							{
								invalidateMeasureOperation = null;
								base.InvalidateMeasure();
								base.InvalidateVisual();
							}
						)
					);
				}
			}
		}

		
		//----------------------------------------
		
		//内部内容（总大于等于窗口大小）的坐标（如果只有垂直滚动条且向下滚，则X总是为0，Y为负数，H大于窗口大小，W等于窗口大小）
      public Rect DisplayRectangle
        {
            get
            {
            	//return new System.Drawing.Rectangle((int)-scrollOffset.X, (int)-scrollOffset.Y, (int)scrollViewport.Width, (int)scrollViewport.Height);
                return new Rect(-scrollOffset.X, -scrollOffset.Y, scrollExtent.Width, scrollExtent.Height);
            }
        }

      //视窗的大小（等于窗口大小）（假设左上角恒定为原点）
        public Size ClientSize 
        {
            get 
            {
                //return new System.Drawing.Size((int)scrollExtent.Width, (int)scrollExtent.Height);
                return new Size(scrollViewport.Width, scrollViewport.Height);
            }
        }

        protected bool HScroll 
        {
            get
            {
                //return canHorizontallyScroll;
                return this.scrollExtent.Width > this.scrollViewport.Width;
            }
        }
        protected bool VScroll 
        {
            get
            {
                //return canVerticallyScroll;
                return this.scrollExtent.Height > this.scrollViewport.Height;
            }
        }

        public void Invalidate()
        {
        	long lastIdle = Environment.TickCount - lastTick;
        	int timeFrame = 1000 / 24; // 
        	if (lastElapsed + lastIdle > timeFrame && lastIdle < timeFrame)
            {
        		Debug.WriteLine("========>skip"); //跳帧
            	lastTick = Environment.TickCount;
            	return; //刷得太快，跳过
            }
            //this.InvalidateMeasure(DispatcherPriority.Render); //FIXME:
            this.InvalidateVisual(); //FIXME:
        }

        public void PerformLayout()
        {
            this.InvalidateMeasure(DispatcherPriority.Render); //FIXME:
        }

        public class SystemInformation
        {
            public static int VerticalScrollBarWidth 
            {
                get
                {
                    return (int)SystemParameters.VerticalScrollBarWidth;
                }
            }

            public static int HorizontalScrollBarHeight
            {
                get
                {
                    return (int)SystemParameters.HorizontalScrollBarHeight;
                }
            }
        }

        public double getWidth()
        {
        	//Debug.WriteLine("this.ActualWidth = " + this.ActualWidth + ", this.RenderSize.Width " + this.RenderSize.Width);
            return this.ActualWidth;
        }

        public double getHeight()
        {
            return this.ActualHeight;
        }
        
      	public void SetDisplayRectLocation(Point offset)
        {
            SetDisplayRectLocation(offset, true);
        }

        public void SetDisplayRectLocation(Point offset, bool preserveContents)
        {
            SetDisplayRectLocation(offset.X, offset.Y);
            this.AdjustFormScrollbars(true);
            this.PerformLayout();
            this.fastDraw();
            this.Invalidate();
        }

        public void SetDisplaySize(Size size, bool isRedraw)
        {
            scrollExtent = new Size(size.Width > 0 ? size.Width : 0, size.Height > 0 ? size.Height : 0);
            this.AdjustFormScrollbars(true);
            if (isRedraw)
            {
            	this.PerformLayout();
            	//this.fastDraw(); //FIXME:don't change fastdraw()
            	this.Invalidate();
            	//this.InvalidateMeasure(DispatcherPriority.Render); //FIXME:
            	//this.InvalidateVisual(); //FIXME:
            }
        }

        protected void AdjustFormScrollbars(bool displayScrollbars)
        {
            //do nothing;
        }

        protected void SetDisplayRectLocation(double x, double y)
        {
            if (-y < 0) y = 0; //FIXME:SetScrollOffset allows minus y
            if (-x < 0) x = 0;
            SetScrollOffset(new Vector(-x, -y));
        }        
        
        public bool Capture {get;set;}
        
        //-------------------------------------------------
		
      	public const double Epsilon = 0.01;
		public static bool IsClose(double d1, double d2)
		{
			if (d1 == d2) // required for infinities
				return true;
			return Math.Abs(d1 - d2) < Epsilon;
		}
		
		public static bool IsClose(Size d1, Size d2)
		{
			return IsClose(d1.Width, d2.Width) && IsClose(d1.Height, d2.Height);
		}
		
		public static bool IsClose(Vector d1, Vector d2)
		{
			return IsClose(d1.X, d2.X) && IsClose(d1.Y, d2.Y);
		}
	}
}
