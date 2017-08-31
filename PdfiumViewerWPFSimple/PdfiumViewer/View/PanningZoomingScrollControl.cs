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
	/// Description of PanningZoomingScrollControl.
	/// </summary>
	public class PanningZoomingScrollControl : CustomScrollControl
	{
		public PanningZoomingScrollControl()
		{
            ZoomFactor = DefaultZoomFactor;
            _zoomMin = DefaultZoomMin;
            _zoomMax = DefaultZoomMax;	
		}
		
        public const double DefaultZoomMin = 0.1;
        public const double DefaultZoomMax = 5;
        public const double DefaultZoomFactor = 1.2;

        private double _zoom = 2.0; //默认放大倍数调这里：1.0->2.0
        private bool _canPan;
        private Point _dragStart;
        private Point _startOffset;
        private double _zoomMax;
        private double _zoomMin;

        public event EventHandler ZoomChanged;

        protected virtual void OnZoomChanged(EventArgs e)
        {
            var ev = ZoomChanged;

            if (ev != null)
                ev(this, e);        	
        }
        
        /// <summary>
        /// Gets or sets the current zoom level.
        /// </summary>
        [Browsable(false)]
        [DefaultValue(2.0)]
        public double Zoom
        {
            get { return _zoom; }
            set
            {
                SetZoom(value, null);
            }
        }
        
        public void setZoom(double value, Point? focus)
        {
        	SetZoom(value, focus);
        }
        
        protected virtual void SetZoom(double value, Point? focus)
        {
        	value = Math.Min(Math.Max(value, ZoomMin), ZoomMax);
            _zoom = value;

            OnZoomChanged(EventArgs.Empty);
			
            this.fastDraw();
            Invalidate();        	
        }
        
        [DefaultValue(DefaultZoomFactor)]
        public double ZoomFactor { get; set; }

        [DefaultValue(DefaultZoomMin)]
        public double ZoomMin
        {
            get { return _zoomMin; }
            set
            {
                _zoomMin = value;
                Zoom = Zoom;
            }
        }

        [DefaultValue(DefaultZoomMax)]
        public double ZoomMax
        {
            get { return _zoomMax; }
            set
            {
                _zoomMax = value;
                Zoom = Zoom;
            }
        }

        /// <summary>
        /// Zooms the PDF document in one step.
        /// </summary>
        public void ZoomIn()
        {
            Zoom *= ZoomFactor;
        }

        /// <summary>
        /// Zooms the PDF document out one step.
        /// </summary>
        public void ZoomOut()
        {
            Zoom /= ZoomFactor;
        }

        [DefaultValue(MouseWheelMode.PanAndZoom)]
        public MouseWheelMode MouseWheelMode { get; set; }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
			bool doZoom;

            switch (MouseWheelMode)
            {
                case MouseWheelMode.PanAndZoom:
            		doZoom = ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control);
                    break;
                case MouseWheelMode.Zoom:
                    doZoom = true;
                    break;
                default:
                    doZoom = false;
                    break;
            }

            if (doZoom)
            {
                double zoom = _zoom;

                if (e.Delta > 0)
                    zoom *= ZoomFactor;
                else
                    zoom /= ZoomFactor;

                zoom = Math.Min(Math.Max(zoom, ZoomMin), ZoomMax);

                //.Offset((int)(-scrollOffset.X), (int)(-scrollOffset.Y))
               	Point pt = e.GetPosition(this);
               	//pt.Offset(scrollOffset.X, scrollOffset.Y);
               	SetZoom(zoom, new Point((pt.X * Zoom), (pt.Y * Zoom)));
                e.Handled = true;
            }
            else
            {
            	base.OnMouseWheel(e);
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
        	base.OnMouseLeftButtonDown(e);
            if (!_canPan)
                return;

            Capture = true;
            Point Location = new Point(e.GetPosition(this).X, e.GetPosition(this).Y);
            _dragStart = Location;
            _startOffset = DisplayRectangle.Location;
        }
        
        protected override void OnMouseMove(MouseEventArgs e)
        {
        	base.OnMouseMove(e);
        	
            if (!Capture)
                return;
			
            Point Location = new Point(e.GetPosition(this).X, e.GetPosition(this).Y);
            
            Point offset = new Point(Location.X - _dragStart.X, Location.Y - _dragStart.Y);

            SetDisplayRectLocation(new Point((_startOffset.X + offset.X), (_startOffset.Y + offset.Y)));
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
        	base.OnMouseLeftButtonUp(e);
        	
        	Capture = false;
        }
        
        protected override void OnLayout()
        {
            //from base class
            //Debug.WriteLine("_canPan w : " + (DisplayRectangle.Width > ClientSize.Width) + "," + DisplayRectangle.Width + "," +  ClientSize.Width);
            //Debug.WriteLine("_canPan h : " + (DisplayRectangle.Height > ClientSize.Height) + "," + DisplayRectangle.Height + "," +  ClientSize.Height);
            _canPan = DisplayRectangle.Width > ClientSize.Width || DisplayRectangle.Height > ClientSize.Height;
            base.OnLayout();
        }
        
        protected override void OnMouseLeave(MouseEventArgs e)
        {
        	base.OnMouseLeave(e);
        	
        	Capture = false;
        }
	}
}
