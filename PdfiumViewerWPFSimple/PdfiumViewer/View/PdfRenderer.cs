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
using System.Threading;
using System.Windows.Input;

namespace PdfiumViewer
{
	public class PdfRenderer : PanningZoomingScrollControl
	{
		private const bool ENABLE_ANNOT = true; // 
		private const bool ENABLE_ANNOT_SCREEN = false; //相对于屏幕
		private const bool ENABLE_ANNOT_PAGE = true; //相对于pdf文档页面
		
		private BackgroundWorker worker = new BackgroundWorker();
		private Stack<LoadQueueItem> pendingQueue = new Stack<LoadQueueItem>(); 
        private object locker = new object();
        //private Dictionary<int, BitmapSource> cacheMap = new Dictionary<int, BitmapSource>(); //FIXME:这里要改成LRU防止内存溢出
        private LRUCache<int, BitmapSource> cacheMap = new LRUCache<int, BitmapSource>(100); //每张图估算是1MB
        
        //annot
        public bool EnableAnnot 
        {
        	get {return enableAnnot;}
        	set {enableAnnot = value; this.InvalidateVisual();}
        }
        private bool enableAnnot = false;
        private double _annotThick = 4.0; //FIXME:目前是个固定值
        private bool isAnnoting = false;
        public double _mouseMoveX = -1;
        public double _mouseMoveY = -1;
        public List<List<Point>> _annotList = new List<List<Point>>();
        public class PageAnnot
        {
        	public int page;
        	public List<Point> pts;
        	
        	public PageAnnot()
        	{
        		this.page = -1;
        		this.pts = new List<Point>();
        	}
        }
        public class PagePoint
        {
        	public double X;
        	public double Y;
        	public int page;
        }
        
        public List<PageAnnot> _pageAnnotList = new List<PageAnnot>();
        
        
        static PdfRenderer()
		{
			FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PdfRenderer), new FrameworkPropertyMetadata(typeof(PdfRenderer)));
		}

		public PdfRenderer()
		{
			this.Loaded += PdfRenderer_Loaded;
			this.Unloaded += PdfRenderer_Unloaded;
			this.SizeChanged += new SizeChangedEventHandler(PdfRenderer_SizeChanged);
            _filename = null;
            
            //注意，请把初始化操作写在Load()
        }

		void PdfRenderer_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			int page = this.Page;
			this.OnLayout();
			this.Page = page; //FIXME:跳回原来的页码
		}

		private void PdfRenderer_Loaded(object sender, RoutedEventArgs e)
		{
			//Debug.WriteLine("Loaded");
			//this.Invalidate();
			this.UpdateScrollbars(true);
			this.worker.WorkerReportsProgress = true;
            this.worker.DoWork += (object sender2, DoWorkEventArgs e2) =>
            {
                DoWork();
            };
            this.worker.RunWorkerCompleted += (object sender3, RunWorkerCompletedEventArgs e3) =>
            {
                if (e3.Error != null)
                {
                	Debug.WriteLine(e3.Error.ToString());
                }
                this.worker.Dispose();
                this.worker = null;
            };
            this.worker.ProgressChanged += delegate(object sender4, ProgressChangedEventArgs e4) {
            	Debug.WriteLine("invalidate");
            	this.fastDraw();
				Invalidate();
            };
            this.worker.RunWorkerAsync();	         
		}
		
		private void PdfRenderer_Unloaded(object sender, RoutedEventArgs e)
		{
        	LoadQueueItem item = new LoadQueueItem();
			item.idx = -1;
			item.isExit = true;
        	lock (locker)
			{
        		pendingQueue.Push(item);
			}  
		}
		
		public PdfRenderer(string filename)
		{
			this.Loaded += PdfRenderer_Loaded;
            _filename = filename;
		}
		
		string _filename;
        private void initFile()
        {
            string message = "";
            //关闭原有文件，打开新文件
            //Load(OpenDocument2(new MemoryStream(PdfiumViewerWPFSimple.Properties.Resources.glm), ref message));
        }

        public static PdfDocument OpenDocument2(Stream LoadStream, ref string message)
        {
            try
            {
                return PdfDocument.Load(LoadStream);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return null;
            }
        } 

		public string Filename
		{
			get
			{
                return _filename;
			}
			set
			{
				_filename = value;
                initFile();
				this.InvalidateMeasure(DispatcherPriority.Render);
			}
		}

        //--------------------------------------------------

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
        /// Gets or sets the currently focused page.
        /// </summary>
        public int Page
        {
            get
            {
                if (Document == null || !_pageCacheValid)
                    return 0;

                double top = -DisplayRectangle.Top;
                double bottom = top + GetScrollClientArea().Height;

                for (int page = 0; page < Document.getPageSizes().Count; page++)
                {
                    var pageCache = _pageCache[page].OuterBounds;
                    if (top - 10 < pageCache.Top)
                    {
                        // If more than 50% of the page is hidden, return the previous page.

                        double hidden = pageCache.Bottom - bottom;
                        if (hidden > 0 && hidden / pageCache.Height > 0.8/*0.5*/ && page > 0)
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
        
        public int TotalPage
        {
        	get
        	{
        		if (Document != null)
        		{
        			return Document.getPageCount();
        		}
        		else
        		{
        			return 0;
        		}
        	}
        }

        /// <summary>
        /// Get the outer bounds of the page.
        /// </summary>
        /// <param name="page">The page to get the bounds for.</param>
        /// <returns>The bounds of the page.</returns>
        public Rect GetOuterBounds(int page)
        {
            if (Document == null || !_pageCacheValid)
                return Rect.Empty;

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
                PageCache pageCache = _pageCache[page];
                Rect rectangle = pageCache.OuterBounds;
                rectangle.Offset(offset.X, offset.Y);

                if (rectangle.Contains(location))
                {
                    var pageBounds = pageCache.Bounds;
                    pageBounds.Offset(offset.X, offset.Y);

                    if (pageBounds.Contains(location))
                    {
                        var size = TranslateSize(Document.getPageSizes()[page]);
                        location = new Point(location.X - pageBounds.Left, location.Y - pageBounds.Top);

                        Point translated = new Point(
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
            pageBounds.Offset(offset.X, offset.Y);

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
        public PdfRectangle BoundsToPdf(Rect bounds)
        {
            if (Document == null)
                return PdfRectangle.Empty;

            Point offset = GetScrollOffset();

            for (int page = 0; page < Document.getPageSizes().Count; page++)
            {
                PageCache pageCache = _pageCache[page];
                Rect rectangle = pageCache.OuterBounds;
                rectangle.Offset(offset.X, offset.Y);

                if (rectangle.IntersectsWith(bounds))
                {
                    Rect pageBounds = pageCache.Bounds;
                    pageBounds.Offset(offset.X, offset.Y);

                    if (pageBounds.IntersectsWith(bounds))
                    {
                        System.Drawing.SizeF size = TranslateSize(Document.getPageSizes()[page]);
                        double scaleX = size.Width / pageBounds.Width;
                        double scaleY = size.Height / pageBounds.Height;

                        return new PdfRectangle(
                            page,
                            new System.Drawing.RectangleF(
                            	(float)((bounds.X - pageBounds.Left) * scaleX),
                                (float)((pageBounds.Height - (bounds.Y - pageBounds.Top)) * scaleY),
                                (float)(bounds.Width * scaleX),
                            	(float)(bounds.Height * scaleY)
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
        public System.Drawing.Rectangle BoundsFromPdf(PdfRectangle bounds)
        {
            Point offset = GetScrollOffset();
            PageCache pageCache = _pageCache[bounds.Page];

            Rect pageBounds = pageCache.Bounds;
            pageBounds.Offset(offset.X, offset.Y);

            var size = TranslateSize(Document.getPageSizes()[bounds.Page]);
            double scaleX = pageBounds.Width / size.Width;
            double scaleY = pageBounds.Height / size.Height;

            return new System.Drawing.Rectangle(
                (int)(pageBounds.X + bounds.Bounds.X * scaleX),
                (int)(pageBounds.Y + (size.Height - bounds.Bounds.Y - bounds.Bounds.Height) * scaleY),
                (int)(bounds.Bounds.Width * scaleX),
                (int)(bounds.Bounds.Height * scaleY)
            );
        }

        private Point GetScrollOffset()
        {
            Rect bounds = GetScrollClientArea();
            double maxWidth = (_maxWidth * _scaleFactor) + PageMargin.Horizontal;
            double leftOffset = (HScroll ? DisplayRectangle.X : (bounds.Width - maxWidth) / 2) + maxWidth / 2;
            double topOffset = VScroll ? DisplayRectangle.Y : 0;

            return new Point(leftOffset, topOffset);
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
			
            _annotList.Clear();
            _annotList.Add(new List<Point>());
            _pageAnnotList.Clear();
            
            Document = document;

            SetDisplayRectLocation(new Point(0, 0));

            ReloadDocument();
        }

        private void ReloadDocument()
        {
        	cacheMap.Clear();
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

            UpdateScrollbars(true);

            this.fastDraw();
            Invalidate();
        }

        private void UpdateScrollbars(bool isRedraw)
        {
            if (Document == null)
                return;

            UpdateScaleFactor(ScrollBars.Both);

            Rect bounds = GetScrollClientArea(ScrollBars.Both);

            Size documentSize = GetDocumentBounds().Size;

            bool horizontalVisible = documentSize.Width > bounds.Width;

            if (!horizontalVisible)
            {
                UpdateScaleFactor(ScrollBars.Vertical);

                documentSize = GetDocumentBounds().Size;
            }

            {
	            _suspendPaintCount++;
	
	            try
	            {
	                SetDisplaySize(documentSize, isRedraw);
	            }
	            finally
	            {
	                _suspendPaintCount--;
	            }
            }

            RebuildPageCache();
        }

        private void RebuildPageCache()
        {
            if (Document == null || _suspendPaintCount > 0)
                return;

            //Debug.WriteLine("RebuildPageCache _scaleFactor : " + _scaleFactor);
            
            _pageCacheValid = true;

            double maxWidth = (_maxWidth * _scaleFactor) + PageMargin.Horizontal;
            double leftOffset = -maxWidth / 2;

            double offset = 0;

            for (int page = 0; page < Document.getPageSizes().Count; page++)
            {
                System.Drawing.SizeF size = TranslateSize(Document.getPageSizes()[page]);
                double height = (size.Height * _scaleFactor);
                double fullHeight = height + PageMargin.Vertical;
                double width = (size.Width * _scaleFactor);
                double maxFullWidth = (_maxWidth * _scaleFactor) + PageMargin.Horizontal;
                double fullWidth = width + PageMargin.Horizontal;
                double thisLeftOffset = leftOffset + (maxFullWidth - fullWidth) / 2;

                while (_pageCache.Count <= page)
                {
                    _pageCache.Add(new PageCache());
                }

                var pageCache = _pageCache[page];

                pageCache.Bounds = new Rect(
                    thisLeftOffset,
                    offset + PageMargin.Top,
                    width,
                    height
                );
                pageCache.OuterBounds = new Rect(
                    thisLeftOffset,
                    offset,
                    width + PageMargin.Horizontal,
                    height + PageMargin.Vertical
                );

                offset += fullHeight;
            }
        }

        private Rect GetScrollClientArea()
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

        private Rect GetScrollClientArea(ScrollBars scrollbars)
        {
            return new Rect(
                0,
                0,
                scrollbars == ScrollBars.Vertical || scrollbars == ScrollBars.Both ? getWidth() - SystemInformation.VerticalScrollBarWidth : getWidth(),
                scrollbars == ScrollBars.Horizontal || scrollbars == ScrollBars.Both ? getHeight() - SystemInformation.HorizontalScrollBarHeight : getHeight()
            );
        }

        private void UpdateScaleFactor(ScrollBars scrollBars)
        {
            Rect bounds = GetScrollClientArea(scrollBars);

            // Scale factor determines what we need to multiply the dimensions
            // of the metafile with to get the size in the control.

            PdfViewerZoomMode zoomMode = CalculateZoomModeForFitBest(bounds);

            if (zoomMode == PdfViewerZoomMode.FitHeight)
            {
                double height = bounds.Height - PageMargin.Vertical;

                _scaleFactor = ((double)height / _maxHeight) * Zoom;
            }
            else
            {
                double width = bounds.Width - PageMargin.Horizontal;

                _scaleFactor = ((double)width / _maxWidth) * Zoom;
            }
        }

        private PdfViewerZoomMode CalculateZoomModeForFitBest(Rect bounds)
        {
            if (ZoomMode != PdfViewerZoomMode.FitBest)
            {
                return ZoomMode;
            }

            var controlScaleFactor = bounds.Width / bounds.Height;

            return controlScaleFactor >= _documentScaleFactor ? PdfViewerZoomMode.FitHeight : PdfViewerZoomMode.FitWidth;
        }


        //FIXME:
        private void DrawPageImage(System.Drawing.Graphics graphics, int page, Rect pageBounds)
        {
            Document.Render(page, graphics, graphics.DpiX, graphics.DpiY, 
        	                new System.Drawing.Rectangle((int)pageBounds.X, (int)pageBounds.Y, (int)pageBounds.Width, (int)pageBounds.Height),
        	                PdfRenderFlags.Annotations);
        }

        private void DrawPageImage(DrawingContext drawingContext, int page, Rect pageBounds, Rect clipRectangle)
        {
        	Rect ir = Rect.Intersect(pageBounds, clipRectangle);
        	
            var image = Document.Render(page, 96, 96, 
        	                         new System.Drawing.Rectangle((int)pageBounds.X, (int)pageBounds.Y, (int)pageBounds.Width, (int)pageBounds.Height),
        	                         new System.Drawing.Rectangle((int)clipRectangle.X, (int)clipRectangle.Y, (int)clipRectangle.Width, (int)clipRectangle.Height), false);
            if (image != null)
            {
	            BitmapSource bitmapSource = BitmapHelper.ToBitmapSource(image);
	            drawingContext.DrawImage(bitmapSource, 
	            	new Rect(ir.X - clipRectangle.X, ir.Y - clipRectangle.Y, 
	                         ir.Width, ir.Height));
            }
		}

        /// <summary>
        /// Gets the document bounds.
        /// </summary>
        /// <returns>The document bounds.</returns>
        protected Rect GetDocumentBounds()
        {
            double height = (_height * _scaleFactor + (PageMargin.Vertical) * Document.getPageCount());
            double width = (_maxWidth * _scaleFactor + PageMargin.Horizontal);

            Point center = new Point(
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

            return new Rect(
                center.X - width / 2,
                center.Y - height / 2,
                width,
                height
            );
        }

        private System.Drawing.SizeF TranslateSize(System.Drawing.SizeF size)
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

            Rect bounds = new Rect(); // 文档坐标
            Rect display = this.DisplayRectangle; //内容坐标
            
            if (focus.HasValue)
            {
                bounds = GetDocumentBounds();

                location = new Point(
                    focus.Value.X - bounds.X,
                    focus.Value.Y - bounds.Y
                );
            }
            else
            {
                bounds = _pageCacheValid
                    ? _pageCache[Page].Bounds
                    : GetDocumentBounds();

                location = new Point(
                    bounds.X,
                    bounds.Y
                );
            }

            double oldScale = Zoom;

            base.SetZoom(zoom, null);

            if (!focus.HasValue)
            {
	            var newLocation = new Point(
	                (location.X * (zoom / oldScale)),
	                (location.Y * (zoom / oldScale))
	            );
            	
            	SetDisplayRectLocation(
	                new Point(
	                    DisplayRectangle.Left - (newLocation.X - location.X),
	                    DisplayRectangle.Top - (newLocation.Y - location.Y)
	                ),
	                false
	            );
            }
            else
            {
            	//(focus - b[new]) / zoom(new) = (focus - b[old]) / zoom[old]
            	double temp = ((focus.Value.Y - (bounds.Y + bounds.Height * 0.5)) / (bounds.Height * 0.5));
            	temp = 0;
            	double targetX = (focus.Value.X - (zoom/oldScale) * (focus.Value.X - bounds.X));
            	double targetY = (focus.Value.Y - (zoom/oldScale) * (focus.Value.Y - bounds.Y));

            	SetDisplayRectLocation(
	                new Point(targetX, targetY),
	                false
	            );
            	Debug.WriteLine("targetX = " + targetX + 
            	                ", targetY = " + targetY + 
            	                ", oldScale = " + oldScale + 
            	                ", zoom = " + zoom +
            	                ", focusX = " + (focus.HasValue ? focus.Value.X : 0) + 
            	                ", focusY = " + (focus.HasValue ? focus.Value.Y : 0) + 
            	                ", display0 = " + display + 
            	                ", bounds0 = " + bounds +
            	                ", display1 = " + this.DisplayRectangle + 
            	                ", bounds1 = " + GetDocumentBounds() +
            	               "");
            }
        }

        private void RedrawMarkers()
        {
        	this.fastDraw();
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
        public void ScrollIntoView(System.Drawing.Rectangle rectangle)
        {
            var clientArea = GetScrollClientArea();

            if (rectangle.Top < 0 || rectangle.Bottom > clientArea.Height)
            {
                Rect displayRectangle = DisplayRectangle;
                double center = rectangle.Top + rectangle.Height / 2;
                double documentCenter = center - displayRectangle.Y;
                double displayCenter = clientArea.Height / 2;
                double offset = documentCenter - displayCenter;

                SetDisplayRectLocation(new Point(
                    displayRectangle.X,
                    -offset
                ));
            }
        }

        private class PageCache
        {
            public Rect Bounds { get; set; }
            public Rect OuterBounds { get; set; }
        }

        private struct Padding
        {   
            private double _left;
            private double _top;
            private double _right;
            private double _bottom;

            public Padding(double all)
            {
                _left = _top = _right = _bottom = all;
            }

            public Padding(double left, double top, double right, double bottom)
            {
                _left = left;
                _top = top;
                _right = right;
                _bottom = bottom;
            }

            public double Horizontal { get { return _left + _right; } }

            public double Vertical { get { return _top + _bottom; } }

            public double Top { get { return _top; } }
        }
        
        protected override void OnPaint(DrawingContext drawingContext)
        {
        	//FIXME:
            //Debug.WriteLine("OnPaint =================1");
            if (Document == null || _suspendPaintCount > 0)
            {
            	lastTick = Environment.TickCount;
                return;
            }
            
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();            
            
            var offset = GetScrollOffset();
            var bounds = GetScrollClientArea();

            var brush = new SolidColorBrush(Colors.LightGray);
            var pen = new Pen(Brushes.Black, 1);
            drawingContext.DrawRectangle(brush, null, new Rect(0, 0, this.RenderSize.Width, this.RenderSize.Height));
            
            _visiblePageStart = -1;
            _visiblePageEnd = -1;

            //Debug.WriteLine("OnPaint =================2 offset = " + offset.Width + "," + offset.Height + ", rendersize = " + (int)this.RenderSize.Width + "," + (int)this.RenderSize.Height);
            
            for (int page = 0; page < Document.getPageSizes().Count; page++)
            {
                var pageCache = _pageCache[page];
                var rectangle = pageCache.OuterBounds;
                //Debug.WriteLine("OnPaint =================2 rectangle = " + rectangle.X + "," + rectangle.Y + "," + rectangle.Width + "," + rectangle.Height);
                rectangle.Offset(offset.X, offset.Y);
				
                if (_visiblePageStart == -1 && rectangle.Bottom >= 0)
                    _visiblePageStart = page;
                if (_visiblePageEnd == -1 && rectangle.Top > bounds.Height)
                    _visiblePageEnd = page - 1;

                //FIXME:
                //if (e.ClipRectangle.IntersectsWith(rectangle))
                Rect ClipRectangle = new Rect(0, 0, this.RenderSize.Width, this.RenderSize.Height);
                if (ClipRectangle.IntersectsWith(rectangle))
                {
                    Rect pageBounds = pageCache.Bounds;
                    //Debug.WriteLine("OnPaint =================2 : " + page + ",[" + pageBounds.X + "," + pageBounds.Y + "," + pageBounds.Width + "," + pageBounds.Height);
                    pageBounds.Offset(offset.X, offset.Y);

                    SolidColorBrush brush2 = new SolidColorBrush(Colors.White);
                    Pen pen2 = new Pen(Brushes.Black, 1);
                    drawingContext.DrawRectangle(brush2, pen2, new Rect(pageBounds.X, pageBounds.Y, pageBounds.Width > 0 ? pageBounds.Width : 0, pageBounds.Height > 0 ? pageBounds.Height : 0));

                    if (!this.isScrolling && !this.isAnnoting)
                    {
	                    //Debug.WriteLine("OnPaint =================3 : " + page + ",[" + pageBounds.X + "," + pageBounds.Y + "," + pageBounds.Width + "," + pageBounds.Height);
	                    //FIXME:
	                    DrawPageImage(drawingContext, page, pageBounds, ClipRectangle);
                    }
                    else
                    {
                    	DrawPageImageCache(drawingContext, page, pageBounds, ClipRectangle);
                    }
                }
            }

            if (ENABLE_ANNOT)
            {
            	Pen pen3 = new Pen(Brushes.Red, _annotThick * this.Zoom);
            	pen3.StartLineCap = PenLineCap.Round;
				pen3.EndLineCap = PenLineCap.Round;
				pen3.LineJoin = PenLineJoin.Round;
				
				if (ENABLE_ANNOT_SCREEN)
				{
					foreach (List<Point> aList in this._annotList)
					{
						for (int i = 0; i < aList.Count; ++i)
						{
							if (i == aList.Count - 1)
							{
								continue;
							}
							drawingContext.DrawLine(pen3, aList[i], aList[i+1]);
						}
					}
				}
				if (ENABLE_ANNOT_PAGE)
				{
					foreach (PageAnnot pageAnnot in this._pageAnnotList)
					{
						for (int i = 0; i < pageAnnot.pts.Count; ++i)
						{
							if (i == pageAnnot.pts.Count - 1)
							{
								continue;
							}
							Point p1 = PointFromPdf2(new PdfPoint(pageAnnot.page, pageAnnot.pts[i]));
							Point p2 = PointFromPdf2(new PdfPoint(pageAnnot.page, pageAnnot.pts[i+1]));                        
							drawingContext.DrawLine(pen3, p1, p2);
						}
					}
				}
				//cursor pen
				if (enableAnnot)
				{
            		drawingContext.DrawEllipse(Brushes.Red, null, new Point(_mouseMoveX, _mouseMoveY), _annotThick * 0.5 * this.Zoom, _annotThick * 0.5 * this.Zoom);
				}
            }            
            
            if (_visiblePageStart == -1)
                _visiblePageStart = 0;
            if (_visiblePageEnd == -1)
                _visiblePageEnd = Document.getPageCount() - 1;
            
           	stopwatch.Stop();
           	lastElapsed = stopwatch.ElapsedMilliseconds;
           	lastTick = Environment.TickCount;
           	if (true)//isScrolling)
           	{
            	Debug.WriteLine("OnPaint time : " + stopwatch.ElapsedMilliseconds + ", isScrolling = " + isScrolling); //这里是输出的总运行秒数,精确到毫秒的
           	}
        }

		public class LoadQueueItem
		{
			public int idx = 0;
			public bool isExit = false;
		}        
        
        private void DoWork()
		{
        	for (;;)
			{
	        	LoadQueueItem item = null;
				lock (locker)
				{
					if (pendingQueue.Count > 0)
					{
						item = pendingQueue.Pop();
						if (cacheMap.ContainsKey(item.idx))
						{
							item = null;
						}
					}
				}
				
				if (item != null && item.idx >= 0)
				{
					process(item);
				}
				else if (item != null && item.isExit)
				{
					break;
				}
				else
				{
					Thread.Sleep(50);
				}
        	}
		}
        
        private void process(LoadQueueItem item)
        {
        	int page = item.idx;
        	if (page >= 0)
        	{
        		System.Drawing.Image image = Document.Render(page, 96, 96, false);
        		BitmapSource bitmapSource = BitmapHelper.ToBitmapSource(image);
        		if (image != null)
        		{
        			lock (locker)
					{
        				if (!cacheMap.ContainsKey(page))
        				{
        					cacheMap.Add(page, bitmapSource);
        					Debug.WriteLine("cacheMap add " + page);
        					//Invalidate();
        					worker.ReportProgress(0);
        				}
        			}
        		}
        	}
        }
        
        private void DrawPageImageCache(DrawingContext drawingContext, int page, Rect pageBounds, Rect clipRectangle)
        {
        	BitmapSource bitmapSource = null;
        	lock (locker)
        	{
        		if (cacheMap.ContainsKey(page))
        		{
        			bitmapSource = cacheMap[page];
        		}
        	}
        	if (bitmapSource == null)
        	{
	        	LoadQueueItem item = new LoadQueueItem();
				item.idx = page;
	        	lock (locker)
				{
	        		pendingQueue.Push(item);
				}  
        	}
        	else
        	{
	            drawingContext.DrawImage(bitmapSource, new Rect(pageBounds.X, pageBounds.Y, pageBounds.Width, pageBounds.Height));
        	}
        }
        
        protected override void OnLayout()
        {
        	//int page = this.Page;
        	base.OnLayout();
            //FIXME: after 
            UpdateScrollbars(true);
            //this.Page = page;
        }

        protected void Dispose(bool disposing) //FIXME:
        {
            //FIXME:xxx
        }

        protected override void OnZoomChanged(EventArgs e)
        {
        	base.OnZoomChanged(e);
            UpdateScrollbars(true);
        }

        public enum ScrollBars
        {
            None = 0,
            Horizontal = 1,
            Vertical = 2,
            Both = 3,
        }

        internal class BitmapHelper
        {
            public static BitmapSource ToBitmapSource(System.Drawing.Image image)
            {
                return ToBitmapSource(image as System.Drawing.Bitmap);
            }

            /// <summary>
            /// Convert an IImage to a WPF BitmapSource. The result can be used in the Set Property of Image.Source
            /// </summary>
            /// <param name="bitmap">The Source Bitmap</param>
            /// <returns>The equivalent BitmapSource</returns>
            private static BitmapSource ToBitmapSource(System.Drawing.Bitmap bitmap)
            {
                if (bitmap == null) return null;

                using (System.Drawing.Bitmap source = (System.Drawing.Bitmap)bitmap.Clone())
                {
                    IntPtr ptr = source.GetHbitmap(); //obtain the Hbitmap

                    BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        ptr,
                        IntPtr.Zero,
                        System.Windows.Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                    NativeMethods.DeleteObject(ptr); //release the HBitmap
                    bs.Freeze();
                    return bs;
                }
            }
            
            static int kkk = 0;

            //see https://stackoverflow.com/questions/28411460/bitmap-graphics-vs-winform-control-graphics
			public static BitmapSource ToBitmapSource2(System.Drawing.Image image)
			{
			    using(MemoryStream memory = new MemoryStream())
			    {
			        image.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
			        //image.Save("kkk-" + (kkk) + ".png");
			        memory.Position = 0;
			        var source = new BitmapImage();
			        source.BeginInit();
			        source.StreamSource = memory;
			        source.CacheOption = BitmapCacheOption.OnLoad;
			        source.EndInit();
			
			        return source;
			    }
			}            
            
            private static BitmapSource ToBitmapSource(byte[] bytes, int width, int height, int dpiX, int dpiY)
            {
                var result = BitmapSource.Create(
                                width,
                                height,
                                dpiX,
                                dpiY,
                                PixelFormats.Bgra32,
                                null /* palette */,
                                bytes,
                                width * 4 /* stride */);
                result.Freeze();

                return result;
            }
        }
        
        
       	public void PdfRenderer_PreviewKeyDown(object sender, KeyEventArgs e)
		{
        	switch (e.Key)
        	{
        		case Key.Up:
        			this.LineUp();
        			break;
        			
        		case Key.Down:
        			this.LineDown();
        			break;
        			
        		case Key.Left:
        			this.ZoomIn();
        			break;
        			
        		case Key.Right:
        			this.ZoomOut();
        			break;
        			
        		case Key.PageUp:
        			this.PageUp();
        			break;
        			
        		case Key.PageDown:
        			this.PageDown();
        			break;
        			
        		case Key.Home:
        			break;
        			
        		case Key.End:
        			break;	
        	}
		}
 
       	
 		//------------------------------------
 		//annot
 		
        private void createAnnotMove()
        {
        	if (enableAnnot)
            {
        		if (_annotList.Count > 0 && _annotList[_annotList.Count - 1] != null)
        		{
        			List<Point> aList = _annotList[_annotList.Count - 1];
        			if (aList.Count > 0)
        			{
        				_annotList.Add(new List<Point>());
        			}
        		}
        		else
        		{
        			_annotList.Add(new List<Point>());
        		}
        		
        		
        		if (_pageAnnotList.Count > 0 && _pageAnnotList[_pageAnnotList.Count - 1] != null)
        		{
        			List<Point> aList = _pageAnnotList[_pageAnnotList.Count - 1].pts;
        			if (aList.Count > 0)
        			{
        				_pageAnnotList.Add(new PageAnnot());
        			}
        		}
        		else
        		{
        			_pageAnnotList.Add(new PageAnnot());
        		}
        	}
        }
        
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
        	if (enableAnnot)
            {
        		_mouseMoveX = e.GetPosition(this).X;
        		_mouseMoveY = e.GetPosition(this).Y;
        		this.InvalidateVisual();
        		
        		if (!Keyboard.IsKeyDown(Key.Space))
        		{
		        	createAnnotMove();
		        	isAnnoting = true;
		        	this.onDrag(e);
			    }
        	}
        	
        	base.OnMouseLeftButtonDown(e);
        }
        
        protected override void OnMouseMove(MouseEventArgs e)
        {
        	//Debug.WriteLine("OnMouseMove x:" + e.GetPosition(this).X + " y:" + e.GetPosition(this).Y);
        	if (enableAnnot)
            {
        		_mouseMoveX = e.GetPosition(this).X;
        		_mouseMoveY = e.GetPosition(this).Y;
        		this.InvalidateVisual();        	
        		
        		if (!Keyboard.IsKeyDown(Key.Space))
        		{
        			if (Capture)
        			{
        				isAnnoting = true;
        				this.onDrag(e);
        			}
        			return;
        		}
        	}
        	
        	base.OnMouseMove(e);
        }
        
        private void onDrag(MouseEventArgs e)
        {	        			
        	//here annot start
        	//Debug.WriteLine("Annot x:" + e.GetPosition(this).X + " y:" + e.GetPosition(this).Y);
			if (_annotList.Count > 0 && _annotList[_annotList.Count - 1] != null)
			{
				List<Point> aList = _annotList[_annotList.Count - 1];
				aList.Add(new Point(e.GetPosition(this).X, e.GetPosition(this).Y));
			}
			if (_pageAnnotList.Count > 0 && _pageAnnotList[_pageAnnotList.Count - 1] != null)
			{
				PagePoint pagePoint = getPagePoint(e.GetPosition(this).X, e.GetPosition(this).Y);
				if (pagePoint != null)
				{
					if (_pageAnnotList[_pageAnnotList.Count - 1].page == -1)
					{
    					_pageAnnotList[_pageAnnotList.Count - 1].page = pagePoint.page;
					}
					else if (_pageAnnotList[_pageAnnotList.Count - 1].page == pagePoint.page)
					{
    					List<Point> aList = _pageAnnotList[_pageAnnotList.Count - 1].pts;
    					aList.Add(new Point(pagePoint.X, pagePoint.Y));
					}
					else
					{
						//FIXME: the stroke is not in the same page
					}
    			}
			}
			//end
        }
        
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
        	isAnnoting = false;
			createAnnotMove();
        	base.OnMouseLeftButtonUp(e);
        }
        
        protected override void OnMouseLeave(MouseEventArgs e)
        {
        	this._mouseMoveX = -1;
        	this._mouseMoveY = -1;
        	
        	isAnnoting = false;
        	createAnnotMove();
        	base.OnMouseLeave(e);
        }
        
        //screen point to page point
        public PagePoint getPagePoint(double x, double y)
        {
//           	for (int page = 0; page < Document.getPageSizes().Count; page++)
//            {
//                var pageCache = _pageCache[page];
//                var rectangle = pageCache.OuterBounds;
//                rectangle.Offset(offset.X, offset.Y);
//            }

			PdfPoint ppt = PointToPdf2(new Point(x, y));

			if (ppt != PdfPoint.Empty)
			{
				PagePoint result = new PagePoint();
        		result.X = ppt.Location.X;
        		result.Y = ppt.Location.Y;
        		result.page = ppt.Page;
        		Debug.WriteLine("page:" + result.page + ",x:" + result.X + ",y:" + result.Y);
				return result;
        	}
			else
			{
				return null;
			}
        }
        
        public bool SaveAnnot(string annotFilename)
        {
			Dictionary<int, List<PageAnnot>> mapPage = new Dictionary<int, List<PageAnnot>>();
			foreach (PageAnnot pageAnnot in this._pageAnnotList)
			{
				if (pageAnnot.page < 0) continue; //maybe -1, only fake
				if (!mapPage.ContainsKey(pageAnnot.page))
				{
					mapPage.Add(pageAnnot.page, new List<PageAnnot>());
				}
				mapPage[pageAnnot.page].Add(pageAnnot);
			}
			
			if (mapPage.Keys.Count > 0)
			{
				FileInfo myFile = new FileInfo(annotFilename); 
				using (StreamWriter sW = myFile.CreateText())
				{
					sW.WriteLine("a" + " " + mapPage.Keys.Count);
					foreach (int page in mapPage.Keys)
					{
						List<PageAnnot> pageAnnotList = mapPage[page];
						int total = 0;
						foreach (PageAnnot pageAnnot in pageAnnotList)
						{
							total += pageAnnot.pts.Count;
						}
						sW.WriteLine("g" + " " + page + " " + pageAnnotList.Count + " " + total);
						foreach (PageAnnot pageAnnot in pageAnnotList)
						{
							sW.WriteLine("c" + " " + pageAnnot.pts.Count);
						}
						foreach (PageAnnot pageAnnot in pageAnnotList)
						{
							for (int i = 0; i < pageAnnot.pts.Count; ++i)
							{
								sW.WriteLine("p" + " " + pageAnnot.pts[i].X + " " + pageAnnot.pts[i].Y);
							}
						}
					}
				}
				return true;
			}
			else
			{
				return false;
			}
        }
        
        
        public PdfPoint PointToPdf2(Point location)
        {
            if (Document == null)
                return PdfPoint.Empty;

            var offset = GetScrollOffset();

            for (int page = 0; page < Document.getPageSizes().Count; page++)
            {
                PageCache pageCache = _pageCache[page];
                Rect rectangle = pageCache.OuterBounds;
                rectangle.Offset(offset.X, offset.Y);

                if (rectangle.Contains(location))
                {
                    var pageBounds = pageCache.Bounds;
                    pageBounds.Offset(offset.X, offset.Y);

                    if (pageBounds.Contains(location))
                    {
                        var size = TranslateSize(Document.getPageSizes()[page]);
                        location = new Point(location.X - pageBounds.Left, location.Y - pageBounds.Top);

                        Point translated = new Point(
                            location.X * (size.Width / pageBounds.Width),
                            (location.Y) * (size.Height / pageBounds.Height)
                        );

                        return new PdfPoint(page, translated);
                    }

                    break;
                }
            }

            return PdfPoint.Empty;
        }
		
        public Point PointFromPdf2(PdfPoint point)
        {
            var offset = GetScrollOffset();
            var pageCache = _pageCache[point.Page];

            var pageBounds = pageCache.Bounds;
            pageBounds.Offset(offset.X, offset.Y);

            var size = TranslateSize(Document.getPageSizes()[point.Page]);
            double scaleX = pageBounds.Width / size.Width;
            double scaleY = pageBounds.Height / size.Height;

            return new Point(
                (int)(pageBounds.X + point.Location.X * scaleX),
                (int)(pageBounds.Y + (point.Location.Y) * scaleY)
            );
        }
        
        public void undoAnnot()
        {
        	int delIndex = -1;
			for (int i = this._pageAnnotList.Count - 1; i >= 0; --i)
			{
				PageAnnot pageAnnot = this._pageAnnotList[i];
				if (pageAnnot.page < 0) continue; //maybe -1, only fake
				if (pageAnnot.pts.Count > 0)
				{
					delIndex = i;
					break;
				}
			}
			if (delIndex >= 0)
			{
				this._pageAnnotList.RemoveAt(delIndex);
				this.InvalidateVisual();
			}
        }
        
        public bool hasAnnot()
        {
			Dictionary<int, List<PageAnnot>> mapPage = new Dictionary<int, List<PageAnnot>>();
			foreach (PageAnnot pageAnnot in this._pageAnnotList)
			{
				if (pageAnnot.page < 0) continue; //maybe -1, only fake
				if (!mapPage.ContainsKey(pageAnnot.page))
				{
					mapPage.Add(pageAnnot.page, new List<PageAnnot>());
				}
				mapPage[pageAnnot.page].Add(pageAnnot);
			}
			
			if (mapPage.Keys.Count > 0)
			{
				return true;
			}
			return false;
        }
	}
}
