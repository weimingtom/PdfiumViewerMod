using System.Drawing;
using System.Windows.Forms;

#pragma warning disable 1591

namespace PdfiumViewer
{
    public class CustomScrollControl : ScrollableControl
    {
        public CustomScrollControl()
        {
            //this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            //this.SetStyle(ControlStyles.Selectable, false);
            //SetStyle(ControlStyles.ContainerControl, true);
            //SetStyle(ControlStyles.AllPaintingInWmPaint, false);
            this.UpdateStyles();
            this.AutoScroll = true;
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
            this.Invalidate();
        }

        public void SetDisplaySize(Size size)
        {
            this.AutoScrollMinSize = size;
            this.AdjustFormScrollbars(true);
            this.PerformLayout();
            this.Invalidate();
        }
    }
}
