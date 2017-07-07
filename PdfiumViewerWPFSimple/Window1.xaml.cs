/*
 * Created by SharpDevelop.
 * User: Administrator
 * Date: 2017-06-20
 * Time: 17:52
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using PdfiumViewer;
using System.Windows.Threading;
using System.Windows.Media.Animation;

//BorderBrush="#FF737070"
namespace PdfiumViewerWPFSimple
{
	/// <summary>
	/// Interaction logic for Window1.xaml
	/// </summary>
	public partial class Window1 : Window
	{
		private DispatcherTimer timer = new DispatcherTimer();
		private bool isSliderEnter = false;
		
		public Window1()
		{
			InitializeComponent();
		}
		
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            pdfRendererer1.Filename = "glm.pdf";
            string message = "";
            Stream LoadStream = new MemoryStream(PdfiumViewerWPFSimple.Properties.Resources.glm);
            pdfRendererer1.ViewArea.PdfRenderer.Load(PdfRenderer.OpenDocument2(LoadStream, ref message)); //FIXME:
            
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += timer_Tick;
            timer.Start();
            this.slider1.DragOver += delegate { 
            	this.isSliderEnter = true; 
            };
           	this.slider1.ValueChanged += new RoutedPropertyChangedEventHandler<double>(Window1_ValueChanged);
           	this.slider1.MouseEnter += delegate {            		
           		this.slider1.Visibility = Visibility.Visible; 
           		this.slider1.Opacity = 1.0;
           		this.isSliderEnter = true;
           	};
           	this.slider1.MouseLeave += delegate { 
				this.isSliderEnter = false;
           	};
           	this.KeyDown += new KeyEventHandler(Window1_KeyDown);
        }

        void Window1_KeyDown(object sender, KeyEventArgs e)
        {
        	this.pdfRendererer1.ViewArea.PdfRenderer.PdfRenderer_PreviewKeyDown(sender, e);
        }

        void Window1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.slider1.Visibility = Visibility.Visible; 
           	this.slider1.Opacity = 1.0;
           		
        	if (this.pdfRendererer1.ViewArea.PdfRenderer.Document != null)
        	{
        		this.pdfRendererer1.ViewArea.PdfRenderer.Page = (int)e.NewValue;
        	}
        }
        
        void timer_Tick(object sender, EventArgs e)
        {
            this.onTick();
        }
        
        private void onTick()
        {
        	bool isScrolling = this.pdfRendererer1.ViewArea.PdfRenderer.isScrolling;
        	int page = this.pdfRendererer1.ViewArea.PdfRenderer.Page;
        	int totalPage = this.pdfRendererer1.ViewArea.PdfRenderer.TotalPage;
        	this.textBlock1.Text = "第" + (page + 1) + "/" + totalPage + "页";
        	this.slider1.Value = page;
        	this.slider1.Maximum = totalPage - 1;
        	
        	//this.border1.Visibility = isScrolling ? Visibility.Visible : Visibility.Hidden;
        	//this.slider1.Visibility = isScrolling ? Visibility.Visible : Visibility.Hidden;
        	if (isScrolling)
        	{
        		this.border1.Visibility = Visibility.Visible;
        		Storyboard fadeInBorder1 = (Storyboard)this.FindResource("FadeInBorder");
        		fadeInBorder1.Begin();
        		if (!this.isSliderEnter)
        		{
	        		this.slider1.Visibility = Visibility.Visible;
	        		Storyboard fadeInBorder2 = (Storyboard)this.FindResource("FadeInBorder2");
	        		fadeInBorder2.Begin();
        		}
        	}
        	else
        	{
        		Storyboard fadeOutBorder1 = (Storyboard)this.FindResource("FadeOutBorder");
        		fadeOutBorder1.Begin();
        		fadeOutBorder1.Completed += fadeOutBorder1_Completed;
        		if (!this.isSliderEnter)
        		{
        			Storyboard fadeOutBorder2 = (Storyboard)this.FindResource("FadeOutBorder2");
	        		fadeOutBorder2.Begin();
	        		fadeOutBorder2.Completed += fadeOutBorder2_Completed;
        		}
        	}
        }

        void fadeOutBorder1_Completed(object sender, EventArgs e)
        {
        	this.border1.Visibility = Visibility.Hidden;
        }

        void fadeOutBorder2_Completed(object sender, EventArgs e)
        {
        	this.slider1.Visibility = Visibility.Hidden;
        }
	}
}