using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using System.Diagnostics;

namespace PdfiumViewer
{
    [TemplatePart(Name = "PART_ScrollViewer", Type = typeof(ScrollViewer))]
    public class PdfViewer : Control
    {
    	private const bool NO_RESUME_MOUSE = true; // don't resume mouse mode, not very good, see scrollViewer_ManipulationStarted e.Cancel = true; 
    	private const bool RESUME_MOUSE = true;
    	
        public PdfViewer()
        {
            ViewArea = new PdfArea();
        }

        public PdfArea ViewArea { get; set; }

        static PdfViewer()
        {
            FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PdfViewer), new FrameworkPropertyMetadata(typeof(PdfViewer)));
        }

        ScrollViewer scrollViewer;
        
        //		TextBlock textBlock;
        /// <summary>
        /// Is called after the template was applied.
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            scrollViewer = (ScrollViewer)base.GetTemplateChild("PART_ScrollViewer");
            addListener();
            IScrollInfo sc = ViewArea as IScrollInfo;
            if (sc != null)
            {
                sc.ScrollOwner = scrollViewer;
            }
            //			textBlock = (TextBlock)Template.FindName("PART_ColumnHeader", this);
            //
            //			textBlock.Text=s_colheader;
        }

        /// <summary>
        /// Gets the scroll viewer used by the text editor.
        /// This property can return null if the template has not been applied / does not contain a scroll viewer.
        /// </summary>
        internal ScrollViewer ScrollViewer
        {
            get { return scrollViewer; }
        }

        /// <summary>
        /// Dependency property for <see cref="HorizontalScrollBarVisibility"/>
        /// </summary>
        public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty = ScrollViewer.HorizontalScrollBarVisibilityProperty.AddOwner(typeof(PdfViewer), new FrameworkPropertyMetadata(ScrollBarVisibility.Auto));

        /// <summary>
        /// Gets/Sets the horizontal scroll bar visibility.
        /// </summary>
        public ScrollBarVisibility HorizontalScrollBarVisibility
        {
            get { return (ScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty); }
            set { SetValue(HorizontalScrollBarVisibilityProperty, value); }
        }

        /// <summary>
        /// Dependency property for <see cref="VerticalScrollBarVisibility"/>
        /// </summary>
        public static readonly DependencyProperty VerticalScrollBarVisibilityProperty = ScrollViewer.VerticalScrollBarVisibilityProperty.AddOwner(typeof(PdfViewer), new FrameworkPropertyMetadata(ScrollBarVisibility.Auto));

        /// <summary>
        /// Gets/Sets the vertical scroll bar visibility.
        /// </summary>
        public ScrollBarVisibility VerticalScrollBarVisibility
        {
            get { return (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty); }
            set { SetValue(VerticalScrollBarVisibilityProperty, value); }
        }

        public string Filename
        {
            get { return (string)ViewArea.GetValue(PdfArea.FilenameProperty); }
            set { ViewArea.SetValue(PdfArea.FilenameProperty, value); }
        }

        public static readonly DependencyProperty ColumnBackgroundProperty =
            PdfArea.ColumnBackgroundProperty.AddOwner(typeof(PdfViewer), new FrameworkPropertyMetadata(SystemColors.ControlBrush));

        [Category("Appearance")]
        public Brush ColumnBackground
        {
            get { return (Brush)GetValue(ColumnBackgroundProperty); }
            set { SetValue(ColumnBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ColumnForegroundProperty = PdfArea.ColumnForegroundProperty.AddOwner(typeof(PdfViewer), new FrameworkPropertyMetadata(Brushes.Black));

        [Category("Appearance")]
        public Brush ColumnForeground
        {
            get { return (Brush)GetValue(ColumnForegroundProperty); }
            set { SetValue(ColumnForegroundProperty, value); }
        }



        protected override void OnDrop(DragEventArgs e)
        {
            if (e.Effects.HasFlag(DragDropEffects.Move))
            {
                var fd = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (fd != null && File.Exists(fd[0]))
                {
                    this.Filename = fd[0];
                }
            }
            base.OnDrop(e);
        }
        
        
              
       	//----------------------------------------------
		

        private void addListener()
        {
            this.scrollViewer.IsManipulationEnabled = true;
            _gestureDetector = new GestureDetector(this.scrollViewer);
            this.scrollViewer.ManipulationStarted += scrollViewer_ManipulationStarted;
            this.scrollViewer.ManipulationStarting += source_ManipulationStarting;
            this.scrollViewer.ManipulationDelta += source_ManipulationDelta;
            this.scrollViewer.ManipulationInertiaStarting += source_ManipulationInertiaStarting;
            this.scrollViewer.ManipulationCompleted += source_ManipulationCompleted;
        }

        void PdfViewer_KeyDown(object sender, KeyEventArgs e)
        {
        	throw new NotImplementedException();
        }

        private void removeListener()
        {
            this.scrollViewer.IsManipulationEnabled = false;
            if (_gestureDetector != null)
            {
                _gestureDetector.unload();
            }
            this.scrollViewer.ManipulationStarted -= scrollViewer_ManipulationStarted;
            this.scrollViewer.ManipulationStarting -= source_ManipulationStarting;
            this.scrollViewer.ManipulationDelta -= source_ManipulationDelta;
            this.scrollViewer.ManipulationInertiaStarting -= source_ManipulationInertiaStarting;
            this.scrollViewer.ManipulationCompleted -= source_ManipulationCompleted;
        }

        void scrollViewer_ManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            var scrollBarWidth = this.scrollViewer.ComputedVerticalScrollBarVisibility == System.Windows.Visibility.Visible ? SystemParameters.VerticalScrollBarWidth : 0;
			scrollBarWidth += 2;
            //Debug.WriteLine(">>>>>>[][]scrollViewer_ManipulationStarted:" + e.ManipulationOrigin.X + ", w=" + (this.ActualWidth - scrollBarWidth * 2));
            //Debug.WriteLine("==================scrollViewer_ManipulationStarted");
            if (e.ManipulationOrigin.X > this.ActualWidth - scrollBarWidth * 2)
            {
                e.Cancel(); //FIXME:mouse is over scrollbar, resume mouse mode
            }
            
            if (NO_RESUME_MOUSE && this.ViewArea.PdfRenderer.EnableAnnot)
        	{
        		this.ViewArea.PdfRenderer.onManiDrag(e.ManipulationOrigin.X,
			                                 e.ManipulationOrigin.Y);
            }
            
            if (RESUME_MOUSE && this.ViewArea.PdfRenderer.EnableAnnot) //FIXME:instead of NO_RESUME_MOUSE
            {
            	e.Cancel();//FIXME:instead of NO_RESUME_MOUSE, resume mouse mode
            }
        }

        private GestureDetector _gestureDetector;

        private double y1 = 0, y2 = 0;
        private double x1 = 0, x2 = 0;

        void source_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
        {        	
        	//Debug.WriteLine("==================source_ManipulationStarting");
        	e.ManipulationContainer = this;
	        e.Handled = true;
	        e.Mode = ManipulationModes.All;
        	
	        if (NO_RESUME_MOUSE && this.ViewArea.PdfRenderer.EnableAnnot)
        	{
        		this.ViewArea.PdfRenderer.onManiDragBegin();
        		e.Handled = false;
	        	e.Mode = ManipulationModes.Translate;
        	}
        }
        
        void source_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
        	//Debug.WriteLine("==================source_ManipulationCompleted");
	        if (NO_RESUME_MOUSE && this.ViewArea.PdfRenderer.EnableAnnot)
        	{
        		this.ViewArea.PdfRenderer.onManiDragEnd();
        	}        	
        }

        //https://github.com/noureldien/Pen/blob/d443d27a48c3be1b8a2b0443f2191659829be424/Pen.Tools/WebfilePanel.xaml.cs
        void source_ManipulationInertiaStarting(object sender, ManipulationInertiaStartingEventArgs e)
        {
        	//Debug.WriteLine("==================source_ManipulationInertiaStarting");
        	
            // adjust the dispalcement behaviour
            // (10 inches * 96 DIPS per inch / 1000ms^2)
            //e.TranslationBehavior = new InertiaTranslationBehavior();¡¡
            //e.TranslationBehavior.InitialVelocity = e.InitialVelocities.LinearVelocity;
            if (!this.ViewArea.PdfRenderer.EnableAnnot)
            {
            	e.TranslationBehavior.DesiredDeceleration = 10.0 * 96.0 / (1000.0 * 1000.0);
            }
            //e.TranslationBehavior.DesiredDisplacement = Math.Abs(e.InitialVelocities.LinearVelocity.Y) * 300;
        }

        //https://github.com/dendyliu/REvan-2.0/blob/0f84747ef0ca6f09db22427dae8cc77efa2e22de/InfluenceDiagram/MainWindow.xaml.cs
        void source_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
        	//Debug.WriteLine("==================source_ManipulationDelta");
        	
            {
                if (_gestureDetector.IsScalingAllowed)
                {
                    double scale = e.DeltaManipulation.Scale.Length / Math.Sqrt(2);
                    //log.Debug(">>>>>>>>>>>>>>>>>>>source_ManipulationDelta:" + scale + ",x=" + e.DeltaManipulation.Scale.X + ",y=" + e.DeltaManipulation.Scale.Y);
                    // Scale
                    double delta = (e.DeltaManipulation.Scale.X + e.DeltaManipulation.Scale.Y) / 2;
                    double oldzoom = this.ViewArea.PdfRenderer.Zoom;
                    double newzoom = delta * this.ViewArea.PdfRenderer.Zoom;
                    {
                        //this.Zoom(newzoom);
//                        this.ViewArea.PdfRenderer.setZoom(newzoom, null);
                        this.ViewArea.PdfRenderer.setZoom(newzoom, new Point(
                        	((e.ManipulationOrigin.X - e.CumulativeManipulation.Translation.X) * oldzoom), 
                        	((e.ManipulationOrigin.Y - e.CumulativeManipulation.Translation.Y) * oldzoom)));
                    }
                    return;
                }
            }
        	
        	//log.Debug(">>>>>>>>>>>>>>>>>>>source_ManipulationDelta");
            {
        		if (_gestureDetector.IsPanningAllowed)
                {
        			if (!this.ViewArea.PdfRenderer.EnableAnnot)
        			{
	                    // Translate
	                    //log.Debug(">>>>>>>>>>>>>>>>>>>source_ManipulationDelta");
	                    //this.source.s
	                    this.scrollViewer.ScrollToHorizontalOffset(this.scrollViewer.HorizontalOffset - e.DeltaManipulation.Translation.X);
	                    this.scrollViewer.ScrollToVerticalOffset(this.scrollViewer.VerticalOffset - e.DeltaManipulation.Translation.Y);
        			}
        			
        			if (NO_RESUME_MOUSE && this.ViewArea.PdfRenderer.EnableAnnot)
        			{
        				//Debug.WriteLine(">>>>>>>>>>>>>>>>>>>source_ManipulationDelta,x=" + e.ManipulationOrigin.X + ",tx=" + e.CumulativeManipulation.Translation.X);
        				this.ViewArea.PdfRenderer.onManiDrag(e.ManipulationOrigin.X,
        				                                 e.ManipulationOrigin.Y);
        			}
        			
                    return;
                }
            }



            {
                if (_gestureDetector.IsRotatingAllowed)
                {
                    // Rotate
                }
            }
        }
    }
}