using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;

namespace PdfiumViewer
{
    /// <summary>
    /// Control to render PDF documents.
    /// </summary>
    public class PdfRenderer : PanningZoomingScrollControl
    {
        private static readonly Padding PageMargin = new Padding(4);

        private int _height;
        private int _maxWidth;
        private int _maxHeight;
        private double _documentScaleFactor;
        private bool _disposed;
        private double _scaleFactor;
        private int _suspendPaintCount;
        private PdfViewerZoomMode _zoomMode;
        private bool _pageCacheValid;
        private readonly List<PageCache> _pageCache = new List<PageCache>();
        private int _visiblePageStart;
        private int _visiblePageEnd;

        /// <summary>
        /// The associated PDF document.
        /// </summary>
        public PdfDocument Document { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user can give the focus to this control using the TAB key.
        /// </summary>
        /// 
        /// <returns>
        /// true if the user can give the focus to the control using the TAB key; otherwise, false. The default is true.Note:This property will always return true for an instance of the <see cref="T:System.Windows.Forms.Form"/> class.
        /// </returns>
        /// <filterpriority>1</filterpriority><PermissionSet><IPermission class="System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="UnmanagedCode, ControlEvidence"/><IPermission class="System.Diagnostics.PerformanceCounterPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/></PermissionSet>
        [DefaultValue(true)]
        public new bool TabStop
        {
            get { return base.TabStop; }
            set { base.TabStop = value; }
        }

        /// <summary>
        /// Gets or sets the currently focused page.
        /// </summary>
        public int Page
        {
            get
            {
                if (Document == null || !_pageCacheValid)
                    return 0;

                int top = -DisplayRectangle.Top;
                int bottom = top + GetScrollClientArea().Height;

                for (int page = 0; page < Document.getPageSizes().Count; page++)
                {
                    var pageCache = _pageCache[page].OuterBounds;
                    if (top - 10 < pageCache.Top)
                    {
                        // If more than 50% of the page is hidden, return the previous page.

                        int hidden = pageCache.Bottom - bottom;
                        if (hidden > 0 && (double)hidden / pageCache.Height > 0.5 && page > 0)
                            return page - 1;

                        return page;
                    }
                }

                return Document.getPageCount() - 1;
            }
            set
            {
                if (Document == null)
                {
                    SetDisplayRectLocation(new Point(0, 0));
                }
                else
                {
                    int page = Math.Min(Math.Max(value, 0), Document.getPageCount() - 1);

                    SetDisplayRectLocation(new Point(0, -_pageCache[page].OuterBounds.Top));
                }
            }
        }

        /// <summary>
        /// Get the outer bounds of the page.
        /// </summary>
        /// <param name="page">The page to get the bounds for.</param>
        /// <returns>The bounds of the page.</returns>
        public Rectangle GetOuterBounds(int page)
        {
            if (Document == null || !_pageCacheValid)
                return Rectangle.Empty;

            page = Math.Min(Math.Max(page, 0), Document.getPageCount() - 1);
            return _pageCache[page].OuterBounds;
        }

        /// <summary>
        /// Gets or sets the way the document should be zoomed initially.
        /// </summary>
        public PdfViewerZoomMode ZoomMode
        {
            get { return _zoomMode; }
            set
            {
                _zoomMode = value;
                PerformLayout();
            }
        }

        /// <summary>
        /// Initializes a new instance of the PdfRenderer class.
        /// </summary>
        public PdfRenderer()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint, true);

            TabStop = true;
        }

        private void Markers_CollectionChanged(object sender, EventArgs e)
        {
            RedrawMarkers();
        }

        /// <summary>
        /// Converts client coordinates to PDF coordinates.
        /// </summary>
        /// <param name="location">Client coordinates to get the PDF location for.</param>
        /// <returns>The location in a PDF page or a PdfPoint with IsValid false when the coordinates do not match a PDF page.</returns>
        public PdfPoint PointToPdf(Point location)
        {
            if (Document == null)
                return PdfPoint.Empty;

            var offset = GetScrollOffset();

            for (int page = 0; page < Document.getPageSizes().Count; page++)
            {
                var pageCache = _pageCache[page];
                var rectangle = pageCache.OuterBounds;
                rectangle.Offset(offset.Width, offset.Height);

                if (rectangle.Contains(location))
                {
                    var pageBounds = pageCache.Bounds;
                    pageBounds.Offset(offset.Width, offset.Height);

                    if (pageBounds.Contains(location))
                    {
                        var size = TranslateSize(Document.getPageSizes()[page]);
                        location = new Point(location.X - pageBounds.Left, location.Y - pageBounds.Top);

                        var translated = new PointF(
                            location.X * (size.Width / pageBounds.Width),
                            (pageBounds.Height - location.Y) * (size.Height / pageBounds.Height)
                        );

                        return new PdfPoint(page, translated);
                    }

                    break;
                }
            }

            return PdfPoint.Empty;
        }

        /// <summary>
        /// Converts a PDF point to a client point.
        /// </summary>
        /// <param name="point">The PDF point to convert.</param>
        /// <returns>The location of the point in client coordinates.</returns>
        public Point PointFromPdf(PdfPoint point)
        {
            var offset = GetScrollOffset();
            var pageCache = _pageCache[point.Page];

            var pageBounds = pageCache.Bounds;
            pageBounds.Offset(offset.Width, offset.Height);

            var size = TranslateSize(Document.getPageSizes()[point.Page]);
            double scaleX = pageBounds.Width / size.Width;
            double scaleY = pageBounds.Height / size.Height;

            return new Point(
                (int)(pageBounds.X + point.Location.X * scaleX),
                (int)(pageBounds.Y + (size.Height - point.Location.Y) * scaleY)
            );
        }

        /// <summary>
        /// Converts client coordinates to PDF bounds.
        /// </summary>
        /// <param name="bounds">The client coordinates to convert.</param>
        /// <returns>The PDF bounds.</returns>
        public PdfRectangle BoundsToPdf(Rectangle bounds)
        {
            if (Document == null)
                return PdfRectangle.Empty;

            var offset = GetScrollOffset();

            for (int page = 0; page < Document.getPageSizes().Count; page++)
            {
                var pageCache = _pageCache[page];
                var rectangle = pageCache.OuterBounds;
                rectangle.Offset(offset.Width, offset.Height);

                if (rectangle.IntersectsWith(bounds))
                {
                    var pageBounds = pageCache.Bounds;
                    pageBounds.Offset(offset.Width, offset.Height);

                    if (pageBounds.IntersectsWith(bounds))
                    {
                        var size = TranslateSize(Document.getPageSizes()[page]);
                        float scaleX = size.Width / pageBounds.Width;
                        float scaleY = size.Height / pageBounds.Height;

                        return new PdfRectangle(
                            page,
                            new RectangleF(
                                (bounds.X - pageBounds.Left) * scaleX,
                                (pageBounds.Height - (bounds.Y - pageBounds.Top)) * scaleY,
                                bounds.Width * scaleX,
                                bounds.Height * scaleY
                            )
                        );
                    }

                    break;
                }
            }

            return PdfRectangle.Empty;
        }

        /// <summary>
        /// Converts PDF bounds to client bounds.
        /// </summary>
        /// <param name="bounds">The PDF bounds to convert.</param>
        /// <returns>The bounds of the PDF bounds in client coordinates.</returns>
        public Rectangle BoundsFromPdf(PdfRectangle bounds)
        {
            var offset = GetScrollOffset();
            var pageCache = _pageCache[bounds.Page];

            var pageBounds = pageCache.Bounds;
            pageBounds.Offset(offset.Width, offset.Height);

            var size = TranslateSize(Document.getPageSizes()[bounds.Page]);
            double scaleX = pageBounds.Width / size.Width;
            double scaleY = pageBounds.Height / size.Height;

            return new Rectangle(
                (int)(pageBounds.X + bounds.Bounds.X * scaleX),
                (int)(pageBounds.Y + (size.Height - bounds.Bounds.Y - bounds.Bounds.Height) * scaleY),
                (int)(bounds.Bounds.Width * scaleX),
                (int)(bounds.Bounds.Height * scaleY)
            );
        }

        private Size GetScrollOffset()
        {
            var bounds = GetScrollClientArea();
            int maxWidth = (int)(_maxWidth * _scaleFactor) + PageMargin.Horizontal;
            int leftOffset = (HScroll ? DisplayRectangle.X : (bounds.Width - maxWidth) / 2) + maxWidth / 2;
            int topOffset = VScroll ? DisplayRectangle.Y : 0;

            return new Size(leftOffset, topOffset);
        }

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.Control.Layout"/> event.
        /// </summary>
        /// <param name="levent">A <see cref="T:System.Windows.Forms.LayoutEventArgs"/> that contains the event data. </param>
        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);

            UpdateScrollbars();
        }

        /// <summary>
        /// Called when the zoom level changes.
        /// </summary>
        /// <param name="e">The event args.</param>
        protected override void OnZoomChanged(EventArgs e)
        {
            base.OnZoomChanged(e);

            UpdateScrollbars();
        }

        /// <summary>
        /// Load a <see cref="IPdfDocument"/> into the control.
        /// </summary>
        /// <param name="document">Document to load.</param>
        public void Load(PdfDocument document)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (document.getPageCount() == 0)
                throw new ArgumentException("Document does not contain any pages", "document");

            Document = document;

            SetDisplayRectLocation(new Point(0, 0));

            ReloadDocument();
        }

        private void ReloadDocument()
        {
            _height = 0;
            _maxWidth = 0;
            _maxHeight = 0;

            foreach (var size in Document.getPageSizes())
            {
                var translated = TranslateSize(size);
                _height += (int)translated.Height;
                _maxWidth = Math.Max((int)translated.Width, _maxWidth);
                _maxHeight = Math.Max((int)translated.Height, _maxHeight);
            }

            _documentScaleFactor = _maxHeight != 0 ? (double)_maxWidth / _maxHeight : 0D;

            UpdateScrollbars();

            Invalidate();
        }

        private void UpdateScrollbars()
        {
            if (Document == null)
                return;

            UpdateScaleFactor(ScrollBars.Both);

            var bounds = GetScrollClientArea(ScrollBars.Both);

            var documentSize = GetDocumentBounds().Size;

            bool horizontalVisible = documentSize.Width > bounds.Width;

            if (!horizontalVisible)
            {
                UpdateScaleFactor(ScrollBars.Vertical);

                documentSize = GetDocumentBounds().Size;
            }

            _suspendPaintCount++;

            try
            {
                SetDisplaySize(documentSize);
            }
            finally
            {
                _suspendPaintCount--;
            }

            RebuildPageCache();
        }

        private void RebuildPageCache()
        {
            if (Document == null || _suspendPaintCount > 0)
                return;

            _pageCacheValid = true;

            double maxW0 = _maxWidth * _scaleFactor + PageMargin.Horizontal;
            int maxWidth = (int)(maxW0 < int.MaxValue ? maxW0 : int.MaxValue);
            int leftOffset = -maxWidth / 2;

            int offset = 0;

            for (int page = 0; page < Document.getPageSizes().Count; page++)
            {
                var size = TranslateSize(Document.getPageSizes()[page]);
                double h0 = size.Height * _scaleFactor;
                int height = (int)(h0 < int.MaxValue ? h0 : int.MaxValue);
                double fullH0 = (double)((double)height + PageMargin.Vertical < int.MaxValue ? height + PageMargin.Vertical : int.MaxValue);
                int fullHeight = (int)(fullH0 < int.MaxValue ? fullH0 : int.MaxValue);
                double w0 = size.Width * _scaleFactor;
                int width = (int)(w0 < int.MaxValue ? w0 : int.MaxValue);
                double max0 = _maxWidth * _scaleFactor + PageMargin.Horizontal;
                int maxFullWidth = (int)(max0 < int.MaxValue ? max0 : int.MaxValue);
                double fullW0 = (double)((double)width + PageMargin.Horizontal < int.MaxValue ? width + PageMargin.Horizontal : int.MaxValue);
                int fullWidth = (int)(fullW0 < int.MaxValue ? fullW0 : int.MaxValue);
                int thisLeftOffset = leftOffset + (maxFullWidth - fullWidth) / 2;

                while (_pageCache.Count <= page)
                {
                    _pageCache.Add(new PageCache());
                }

                var pageCache = _pageCache[page];

                double ff0 = (double)offset + PageMargin.Top;
                pageCache.Bounds = new Rectangle(
                    thisLeftOffset,
                    (ff0 < (double)int.MaxValue ? (int)ff0 : int.MaxValue),
                    width,
                    height
                );
                double ww0 = (double)width + PageMargin.Horizontal;
                double hh0 = (double)height + PageMargin.Vertical;
                pageCache.OuterBounds = new Rectangle(
                    thisLeftOffset,
                    offset,
                    (ww0 < (double)int.MaxValue ? (int)ww0 : int.MaxValue),
                    (hh0 < (double)int.MaxValue ? (int)hh0 : int.MaxValue)
                );

                if ((double)offset + (double)fullHeight < int.MaxValue)
                {
                	offset += fullHeight;
                }
                else
                {
                	offset = int.MaxValue;
                }
            }
        }

        private Rectangle GetScrollClientArea()
        {
            ScrollBars scrollBarsVisible;

            if (HScroll && VScroll)
                scrollBarsVisible = ScrollBars.Both;
            else if (HScroll)
                scrollBarsVisible = ScrollBars.Horizontal;
            else if (VScroll)
                scrollBarsVisible = ScrollBars.Vertical;
            else
                scrollBarsVisible = ScrollBars.None;

            return GetScrollClientArea(scrollBarsVisible);
        }

        private Rectangle GetScrollClientArea(ScrollBars scrollbars)
        {
            return new Rectangle(
                0,
                0,
                scrollbars == ScrollBars.Vertical || scrollbars == ScrollBars.Both ? Width - SystemInformation.VerticalScrollBarWidth : Width,
                scrollbars == ScrollBars.Horizontal || scrollbars == ScrollBars.Both ? Height - SystemInformation.HorizontalScrollBarHeight : Height
            );
        }

        private void UpdateScaleFactor(ScrollBars scrollBars)
        {
            var bounds = GetScrollClientArea(scrollBars);

            // Scale factor determines what we need to multiply the dimensions
            // of the metafile with to get the size in the control.

            var zoomMode = CalculateZoomModeForFitBest(bounds);

            if (zoomMode == PdfViewerZoomMode.FitHeight)
            {
                int height = bounds.Height - PageMargin.Vertical;

                _scaleFactor = ((double)height / _maxHeight) * Zoom;
            }
            else
            {
                int width = bounds.Width - PageMargin.Horizontal;

                _scaleFactor = ((double)width / _maxWidth) * Zoom;
            }
        }

        private PdfViewerZoomMode CalculateZoomModeForFitBest(Rectangle bounds)
        {
            if (ZoomMode != PdfViewerZoomMode.FitBest)
            {
                return ZoomMode;
            }

            var controlScaleFactor = (double)bounds.Width / bounds.Height;

            return controlScaleFactor >= _documentScaleFactor ? PdfViewerZoomMode.FitHeight : PdfViewerZoomMode.FitWidth;
        }

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.Control.Paint"/> event.
        /// </summary>
        /// <param name="e">A <see cref="T:System.Windows.Forms.PaintEventArgs"/> that contains the event data. </param>
        protected override void OnPaint(PaintEventArgs e)
        {
            //Debug.WriteLine("OnPaint =================1");
            if (Document == null || _suspendPaintCount > 0)
                return;
            
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            var offset = GetScrollOffset();
            var bounds = GetScrollClientArea();

            using (var brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(brush, e.ClipRectangle);
            }

            _visiblePageStart = -1;
            _visiblePageEnd = -1;

            for (int page = 0; page < Document.getPageSizes().Count; page++)
            {
                var pageCache = _pageCache[page];
                var rectangle = pageCache.OuterBounds;
                rectangle.Offset(offset.Width, offset.Height);

                if (_visiblePageStart == -1 && rectangle.Bottom >= 0)
                    _visiblePageStart = page;
                if (_visiblePageEnd == -1 && rectangle.Top > bounds.Height)
                    _visiblePageEnd = page - 1;

                if (e.ClipRectangle.IntersectsWith(rectangle))
                {
                    var pageBounds = pageCache.Bounds;
                    //Debug.WriteLine("OnPaint =================2 : " + page + ",[" + pageBounds.X + "," + pageBounds.Y + "," + pageBounds.Width + "," + pageBounds.Height);
                    pageBounds.Offset(offset.Width, offset.Height);

                    e.Graphics.FillRectangle(Brushes.White, pageBounds);

                    //Debug.WriteLine("OnPaint =================3 : " + page + ",[" + pageBounds.X + "," + pageBounds.Y + "," + pageBounds.Width + "," + pageBounds.Height);
                    DrawPageImage(e.Graphics, page, pageBounds);
                }
            }

            if (_visiblePageStart == -1)
                _visiblePageStart = 0;
            if (_visiblePageEnd == -1)
                _visiblePageEnd = Document.getPageCount() - 1;

            stopwatch.Stop();
            Debug.WriteLine("OnPaint time : " + stopwatch.Elapsed.TotalSeconds); //这里是输出的总运行秒数,精确到毫秒的
        }

        private void DrawPageImage(Graphics graphics, int page, Rectangle pageBounds)
        {
            Document.Render(page, graphics, graphics.DpiX, graphics.DpiY, pageBounds, PdfRenderFlags.Annotations);
        }

        /// <summary>
        /// Gets the document bounds.
        /// </summary>
        /// <returns>The document bounds.</returns>
        protected override Rectangle GetDocumentBounds()
        {
        	double h0 = (_height * _scaleFactor + (PageMargin.Vertical) * Document.getPageCount());
        	double w0 = _maxWidth * _scaleFactor + PageMargin.Horizontal;
        	int height = (int)(h0 < int.MaxValue ? h0 : int.MaxValue);
        	int width = (int)(w0 < int.MaxValue ? w0 : int.MaxValue);
            
            var center = new Point(
                DisplayRectangle.Width / 2,
                DisplayRectangle.Height / 2
            );

            if (
                DisplayRectangle.Width > ClientSize.Width ||
                DisplayRectangle.Height > ClientSize.Height
            ) {
                center.X += DisplayRectangle.Left;
                center.Y += DisplayRectangle.Top;
            }

            return new Rectangle(
                center.X - width / 2,
                center.Y - height / 2,
                width,
                height
            );
        }

        private SizeF TranslateSize(SizeF size)
        {
            return size;
        }

        /// <summary>
        /// Called when the zoom level changes.
        /// </summary>
        /// <param name="zoom">The new zoom level.</param>
        /// <param name="focus">The location to focus on.</param>
        protected override void SetZoom(double zoom, Point? focus)
        {
            Point location;

            if (focus.HasValue)
            {
                var bounds = GetDocumentBounds();

                location = new Point(
                    focus.Value.X - bounds.X,
                    focus.Value.Y - bounds.Y
                );
            }
            else
            {
                var bounds = _pageCacheValid
                    ? _pageCache[Page].Bounds
                    : GetDocumentBounds();

                location = new Point(
                    bounds.X,
                    bounds.Y
                );
            }

            double oldScale = Zoom;

            base.SetZoom(zoom, null);

            var newLocation = new Point(
                (int)(location.X * (zoom / oldScale)),
                (int)(location.Y * (zoom / oldScale))
            );

            SetDisplayRectLocation(
                new Point(
                    DisplayRectangle.Left - (newLocation.X - location.X),
                    DisplayRectangle.Top - (newLocation.Y - location.Y)
                ),
                false
            );
        }

        private void RedrawMarkers()
        {
            Invalidate();
        }

        /// <summary>
        /// Scroll the PDF bounds into view.
        /// </summary>
        /// <param name="bounds">The PDF bounds to scroll into view.</param>
        public void ScrollIntoView(PdfRectangle bounds)
        {
            ScrollIntoView(BoundsFromPdf(bounds));
        }

        /// <summary>
        /// Scroll the client rectangle into view.
        /// </summary>
        /// <param name="rectangle">The client rectangle to scroll into view.</param>
        public void ScrollIntoView(Rectangle rectangle)
        {
            var clientArea = GetScrollClientArea();

            if (rectangle.Top < 0 || rectangle.Bottom > clientArea.Height)
            {
                var displayRectangle = DisplayRectangle;
                int center = rectangle.Top + rectangle.Height / 2;
                int documentCenter = center - displayRectangle.Y;
                int displayCenter = clientArea.Height / 2;
                int offset = documentCenter - displayCenter;

                SetDisplayRectLocation(new Point(
                    displayRectangle.X,
                    -offset
                ));
            }
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="T:System.Windows.Forms.Control"/> and its child controls and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources. </param>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        private class PageCache
        {
            public Rectangle Bounds { get; set; }
            public Rectangle OuterBounds { get; set; }
        }
    }
}
