using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace PdfiumViewer
{
    /// <summary>
    /// Provides functionality to render a PDF document.
    /// </summary>
    public class PdfDocument : IDisposable //FIXME:add IDisposable
    {
        public int getPageCount() { return PageCount; }

        public IList<SizeF> getPageSizes() { return PageSizes; }

        private bool _disposed;
        private PdfFile _file;
        private readonly List<SizeF> _pageSizes;

        /// <summary>
        /// Initializes a new instance of the PdfDocument class with the provided stream.
        /// </summary>
        /// <param name="stream">Stream for the PDF document.</param>
        public static PdfDocument Load(Stream stream)
        {
            return Load(stream, null);
        }

        /// <summary>
        /// Initializes a new instance of the PdfDocument class with the provided stream.
        /// </summary>
        /// <param name="stream">Stream for the PDF document.</param>
        /// <param name="password">Password for the PDF document.</param>
        public static PdfDocument Load(Stream stream, string password)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            return new PdfDocument(stream, password);
        }

        /// <summary>
        /// Number of pages in the PDF document.
        /// </summary>
        public int PageCount
        {
            get { return PageSizes.Count; }
        }

        /// <summary>
        /// Size of each page in the PDF document.
        /// </summary>
        public IList<SizeF> PageSizes;

        private PdfDocument(Stream stream, string password)
        {
            _file = new PdfFile(stream, password);

            _pageSizes = _file.GetPDFDocInfo();
            if (_pageSizes == null)
                throw new Win32Exception();

            PageSizes = new ReadOnlyCollection<SizeF>(_pageSizes);
        }


        /// <summary>
        /// Renders a page of the PDF document to the provided graphics instance.
        /// </summary>
        /// <param name="page">Number of the page to render.</param>
        /// <param name="graphics">Graphics instance to render the page on.</param>
        /// <param name="dpiX">Horizontal DPI.</param>
        /// <param name="dpiY">Vertical DPI.</param>
        /// <param name="bounds">Bounds to render the page in.</param>
        /// <param name="flags">Flags used to influence the rendering.</param>
        public void Render(int page, Graphics graphics, float dpiX, float dpiY, RectangleF bounds, PdfRenderFlags flags)
        {
            if (graphics == null)
                throw new ArgumentNullException("graphics");
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);

            float graphicsDpiX = graphics.DpiX;
            float graphicsDpiY = graphics.DpiY;

            var dc = graphics.GetHdc();

            try
            {
                if ((int)graphicsDpiX != (int)dpiX || (int)graphicsDpiY != (int)dpiY)
                {
                    var transform = new NativeMethods.XFORM
                    {
                        eM11 = graphicsDpiX / dpiX,
                        eM22 = graphicsDpiY / dpiY
                    };

                    NativeMethods.SetGraphicsMode(dc, NativeMethods.GM_ADVANCED);
                    NativeMethods.ModifyWorldTransform(dc, ref transform, NativeMethods.MWT_LEFTMULTIPLY);
                }

                var point = new NativeMethods.POINT();
                NativeMethods.SetViewportOrgEx(dc, (int)bounds.X, (int)bounds.Y, out point);

                bool success = _file.RenderPDFPageToDC(
                    page,
                    dc,
                    (int)dpiX, (int)dpiY,
                    0, 0, (int)bounds.Width, (int)bounds.Height,
                    FlagsToFPDFFlags(flags)
                );

                NativeMethods.SetViewportOrgEx(dc, point.X, point.Y, out point);

                if (!success)
                    throw new Win32Exception();
            }
            finally
            {
                graphics.ReleaseHdc(dc);
            }
        }

        public Image Render(int page, float dpiX, float dpiY, bool forPrinting)
        {
            var size = PageSizes[page];

            double scale = 1; //FIXME: for acceleration, use 1.0 / 2;
            return Render(page, dpiX, dpiY, 
                          new System.Drawing.RectangleF(0, 0, (float)(size.Width / scale), (float)(size.Height / scale)), 
                          new System.Drawing.RectangleF(0, 0, (float)(size.Width / scale), (float)(size.Height / scale)), forPrinting);
        }
        
        /// <summary>
        /// Renders a page of the PDF document to an image.
        /// </summary>
        /// <param name="page">Number of the page to render.</param>
        /// <param name="dpiX">Horizontal DPI.</param>
        /// <param name="dpiY">Vertical DPI.</param>
        /// <param name="forPrinting">Render the page for printing.</param>
        /// <returns>The rendered image.</returns>
        public Image Render(int page, float dpiX, float dpiY, System.Drawing.RectangleF pageBounds, System.Drawing.RectangleF clipRectangle, bool forPrinting)
        {
            //var size = PageSizes[page];

            //return Render(page, (int)size.Width, (int)size.Height, dpiX, dpiY, forPrinting);
            return Render(page, pageBounds, clipRectangle, dpiX, dpiY, forPrinting);
        }

        /// <summary>
        /// Renders a page of the PDF document to an image.
        /// </summary>
        /// <param name="page">Number of the page to render.</param>
        /// <param name="dpiX">Horizontal DPI.</param>
        /// <param name="dpiY">Vertical DPI.</param>
        /// <param name="flags">Flags used to influence the rendering.</param>
        /// <returns>The rendered image.</returns>
        public Image Render(int page, float dpiX, float dpiY, System.Drawing.RectangleF pageBounds, System.Drawing.RectangleF clipRectangle, PdfRenderFlags flags)
        {
            //var size = PageSizes[page];

            //return Render(page, (int)size.Width, (int)size.Height, dpiX, dpiY, flags);
            return Render(page, pageBounds, clipRectangle, dpiX, dpiY, flags);
        }

        /// <summary>
        /// Renders a page of the PDF document to an image.
        /// </summary>
        /// <param name="page">Number of the page to render.</param>
        /// <param name="width">Width of the rendered image.</param>
        /// <param name="height">Height of the rendered image.</param>
        /// <param name="dpiX">Horizontal DPI.</param>
        /// <param name="dpiY">Vertical DPI.</param>
        /// <param name="forPrinting">Render the page for printing.</param>
        /// <returns>The rendered image.</returns>
        public Image Render(int page, System.Drawing.RectangleF pageBounds, System.Drawing.RectangleF clipRectangle, float dpiX, float dpiY, bool forPrinting)
        {
            return Render(page, pageBounds, clipRectangle, dpiX, dpiY, forPrinting ? PdfRenderFlags.ForPrinting : PdfRenderFlags.Annotations);
        }

        /// <summary>
        /// Renders a page of the PDF document to an image.
        /// </summary>
        /// <param name="page">Number of the page to render.</param>
        /// <param name="width">Width of the rendered image.</param>
        /// <param name="height">Height of the rendered image.</param>
        /// <param name="dpiX">Horizontal DPI.</param>
        /// <param name="dpiY">Vertical DPI.</param>
        /// <param name="flags">Flags used to influence the rendering.</param>
        /// <returns>The rendered image.</returns>
        public Image Render(int page, System.Drawing.RectangleF pageBounds, System.Drawing.RectangleF clipRectangle, float dpiX, float dpiY, PdfRenderFlags flags)
        {        	
        	RectangleF ir = RectangleF.Intersect(pageBounds, clipRectangle);

        	double scale = 1;
        	int width = (int)(ir.Width / scale);//pageBounds.Width;
        	int height = (int)(ir.Height / scale);//pageBounds.Height;
        	
        	if (width == 0 || height == 0) return null;
        	
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
			
            if ((flags & PdfRenderFlags.CorrectFromDpi) != 0)
            {
                width = width / 72 * (int)dpiX;
                height = height / 72 * (int)dpiY;
            }

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            //var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            bitmap.SetResolution(dpiX, dpiY);
			
            var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, bitmap.PixelFormat);

            try
            {
                var handle = NativeMethods.FPDFBitmap_CreateEx(width, height, 4, data.Scan0, width * 4);

                try
                {
                    uint background = (flags & PdfRenderFlags.Transparent) == 0 ? 0xFFFFFFFF : 0x00FFFFFF;

                    NativeMethods.FPDFBitmap_FillRect(handle, 0, 0, width, height, background);

                    bool success = _file.RenderPDFPageToBitmap(
                        page,
                        handle,
                        (int)dpiX, (int)dpiY,
                        (int)(-(ir.X - pageBounds.X)), (int)(-(ir.Y - pageBounds.Y)), (int)(pageBounds.Width / scale), (int)(pageBounds.Height / scale),
                        //0, 0, width, height,
                        FlagsToFPDFFlags(flags)
                    );

                    if (!success)
                        throw new Win32Exception();
                }
                finally
                {
                    NativeMethods.FPDFBitmap_Destroy(handle);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }

        private NativeMethods.FPDF FlagsToFPDFFlags(PdfRenderFlags flags)
        {
            return (NativeMethods.FPDF)(flags & ~PdfRenderFlags.Transparent);
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <param name="disposing">Whether this method is called from Dispose.</param>
        protected void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                if (_file != null)
                {
                    _file.Dispose();
                    _file = null;
                }

                _disposed = true;
            }
        }
    }
}
