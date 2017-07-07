using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

#pragma warning disable 1591

namespace PdfiumViewer
{
    public abstract class PanningZoomingScrollControl : CustomScrollControl
    {
        public const double DefaultZoomMin = 0.1;
        public const double DefaultZoomMax = 5;
        public const double DefaultZoomFactor = 1.2;

        static PanningZoomingScrollControl()
        {

        }

        private double _zoom = 1;
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
        [DefaultValue(1.0)]
        public double Zoom
        {
            get { return _zoom; }
            set
            {
                value = Math.Min(Math.Max(value, ZoomMin), ZoomMax);

                SetZoom(value, null);
            }
        }

        protected virtual void SetZoom(double value, Point? focus)
        {
            _zoom = value;

            OnZoomChanged(EventArgs.Empty);

            Invalidate();
        }

        [DefaultValue(DefaultZoomFactor)]
        public double ZoomFactor { get; set; }

        protected PanningZoomingScrollControl()
        {
            ZoomFactor = DefaultZoomFactor;
            _zoomMin = DefaultZoomMin;
            _zoomMax = DefaultZoomMax;
        }

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

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.Control.MouseWheel"/> event.
        /// </summary>
        /// <param name="e">A <see cref="T:System.Windows.Forms.MouseEventArgs"/> that contains the event data. </param>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            bool doZoom;

            switch (MouseWheelMode)
            {
                case MouseWheelMode.PanAndZoom:
                    doZoom = (ModifierKeys & Keys.Control) != 0;
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

                SetZoom(zoom, e.Location);
            }
            else
            {
                base.OnMouseWheel(e);
            }
        }

        protected abstract Rectangle GetDocumentBounds();


        protected override void OnLayout(LayoutEventArgs levent)
        {
            _canPan = DisplayRectangle.Width > ClientSize.Width || DisplayRectangle.Height > ClientSize.Height;

            base.OnLayout(levent);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left || !_canPan)
                return;

            Capture = true;
            _dragStart = e.Location;
            _startOffset = DisplayRectangle.Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!Capture)
                return;

            var offset = new Point(e.Location.X - _dragStart.X, e.Location.Y - _dragStart.Y);

            SetDisplayRectLocation(new Point(_startOffset.X + offset.X, _startOffset.Y + offset.Y));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            Capture = false;
        }
    }
}
