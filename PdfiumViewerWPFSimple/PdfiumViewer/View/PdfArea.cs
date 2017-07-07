using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;

namespace PdfiumViewer
{
	[TemplatePart(Name = "PART_PdfRenderer", Type = typeof(PdfRenderer))]
	public class PdfArea : Control, IScrollInfo
	{
		
		public PdfArea()
		{
			Typeface tf = this.GetTypeface();
			m_filename = null;
			m_linePos = 0;
		}
		
		static PdfArea()
		{
			FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PdfArea), new FrameworkPropertyMetadata(typeof(PdfArea)));
		}
		
		string m_filename;
		public int m_linePos;

		PdfRenderer pdfRenderer;

		public PdfRenderer PdfRenderer
		{
			get
			{
				return pdfRenderer;
			}
		}

		public static readonly DependencyProperty LineHeightProperty =
			DependencyProperty.Register("LineHeight", typeof(double), typeof(PdfArea),
			                            new FrameworkPropertyMetadata(12.0d));
		
		public static readonly DependencyProperty ColumnWidthProperty =
			DependencyProperty.Register("ColumnWidth", typeof(double), typeof(PdfArea),
			                            new FrameworkPropertyMetadata(24.0d));
		
		public static readonly DependencyProperty ColumnHeightProperty =
			DependencyProperty.Register("ColumnHeight", typeof(double), typeof(PdfArea),
			                            new FrameworkPropertyMetadata(24.0d));
		
		/// <summary>
		/// 行高
		/// </summary>
		public double LineHeight{
			get{ return (double)GetValue(LineHeightProperty);}
			set{ SetValue(LineHeightProperty,value);}
		}
		
		/// <summary>
		/// 列宽
		/// </summary>
		public double ColumnWidth{
			get{ return (double)GetValue(ColumnWidthProperty);}
			set{ SetValue(ColumnWidthProperty,value);}
		}
		
		/// <summary>
		/// 获取或设置标题栏高度
		/// </summary>
		public double ColumnHeight{
			get{ return (double)GetValue(ColumnHeightProperty);}
			set{ SetValue(ColumnHeightProperty,value);}
		}

		public static readonly DependencyProperty FilenameProperty =
			DependencyProperty.Register("Filename", typeof(string), typeof(PdfArea),
			                            new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnBaseStreamChanged)));

		public Stream BaseStream
		{
			get { return (Stream)GetValue(FilenameProperty); }
			set { SetValue(FilenameProperty, value); }
		}

		static void OnBaseStreamChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((PdfArea)d).OnBaseStreamChanged((string)e.NewValue);
		}

		void OnBaseStreamChanged(string filename)
		{
            this.pdfRenderer.Filename = filename;
		}
		
		public static readonly DependencyProperty ColumnCountProperty =
			DependencyProperty.Register("ColumnCount", typeof(int), typeof(PdfArea),
			                            new FrameworkPropertyMetadata(16,null,OnCoerceColumnCountValue));
		
		public const int MiniumColumnCount =4;
		public const int MaxiumColumnCount =64;
		
		static object OnCoerceColumnCountValue(DependencyObject d, object basevalue) {
			int val=(int)basevalue;
			if (val < MiniumColumnCount)
				return MiniumColumnCount;
			if (val > MaxiumColumnCount)
				return MaxiumColumnCount;
			return val;
		}

		/// <summary>
		/// 列数4-64之间，默认值为16
		/// </summary>
		[Category("Layout")]
		public int ColumnCount
		{
			get { return (int)GetValue(ColumnCountProperty); }
			set { SetValue(ColumnCountProperty, value); }
		}
		
		public static readonly DependencyProperty AddressWidthProperty =
			DependencyProperty.Register("AddressWidth", typeof(int), typeof(PdfArea),
			                            new FrameworkPropertyMetadata(8,null,OnCoerceAddressWidthValue));
		
		public const int MiniumAddressWidth =4;
		public const int MaxiumAddressWidth =16;
		
		static object OnCoerceAddressWidthValue(DependencyObject d, object basevalue) {
			int val=(int)basevalue;
			if (val < MiniumAddressWidth)
				return MiniumAddressWidth;
			if (val > MaxiumAddressWidth)
				return MaxiumAddressWidth;
			return val;
		}
		
		/// <summary>
		/// 地址栏的宽度4-16之间，默认值为8
		/// </summary>
		[Category("Layout")]
		public int AddressWidth
		{
			get { return (int)GetValue(AddressWidthProperty); }
			set { SetValue(AddressWidthProperty, value); }
		}

		public static readonly DependencyProperty ColumnBackgroundProperty =
			DependencyProperty.Register("ColumnBackground", typeof(Brush), typeof(PdfArea),
			                            new FrameworkPropertyMetadata(SystemColors.ControlBrush));
		[Category("Appearance")]
		public Brush ColumnBackground
		{
			get { return (Brush)GetValue(ColumnBackgroundProperty); }
			set { SetValue(ColumnBackgroundProperty, value); }
		}

		public static readonly DependencyProperty ColumnForegroundProperty =
			DependencyProperty.Register("ColumnForeground", typeof(Brush), typeof(PdfArea),
			                            new FrameworkPropertyMetadata(Brushes.Black));
		[Category("Appearance")]
		public Brush ColumnForeground
		{
			get { return (Brush)GetValue(ColumnForegroundProperty); }
			set { SetValue(ColumnForegroundProperty, value); }
		}

		/// <summary>
		/// The <see cref="SelectionBrush"/> property.
		/// </summary>
		public static readonly DependencyProperty SelectionBrushProperty =
			DependencyProperty.Register("SelectionBrush", typeof(Brush), typeof(PdfArea));

		/// <summary>
		/// Gets/Sets the background brush used for the selection.
		/// </summary>
		public Brush SelectionBrush
		{
			get { return (Brush)GetValue(SelectionBrushProperty); }
			set { SetValue(SelectionBrushProperty, value); }
		}

		/// <summary>
		/// The <see cref="SelectionForeground"/> property.
		/// </summary>
		public static readonly DependencyProperty SelectionForegroundProperty =
			DependencyProperty.Register("SelectionForeground", typeof(Brush), typeof(PdfArea));

		/// <summary>
		/// Gets/Sets the foreground brush used selected text.
		/// </summary>
		public Brush SelectionForeground
		{
			get { return (Brush)GetValue(SelectionForegroundProperty); }
			set { SetValue(SelectionForegroundProperty, value); }
		}

		/// <summary>
		/// The <see cref="SelectionBorder"/> property.
		/// </summary>
		public static readonly DependencyProperty SelectionBorderProperty =
			DependencyProperty.Register("SelectionBorder", typeof(Pen), typeof(PdfArea));

		/// <summary>
		/// Gets/Sets the background brush used for the selection.
		/// </summary>
		public Pen SelectionBorder
		{
			get { return (Pen)GetValue(SelectionBorderProperty); }
			set { SetValue(SelectionBorderProperty, value); }
		}

		public static readonly DependencyProperty FontTypefaceProperty =
			DependencyProperty.Register("FontTypeface", typeof(Typeface), typeof(PdfArea));
		
		public Typeface FontTypeface{
			get { return (Typeface)GetValue(FontTypefaceProperty); }
			set { SetValue(FontTypefaceProperty, value); }
		}
		

		Typeface GetTypeface()
		{
			return new Typeface(this.FontFamily, this.FontStyle, this.FontWeight, this.FontStretch);
		}
		
		protected override Size ArrangeOverride(Size arrangeBounds)
		{
			arrangeBounds = base.ArrangeOverride(arrangeBounds);
			return arrangeBounds;
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			pdfRenderer = base.GetTemplateChild("PART_PdfRenderer") as PdfRenderer;
			if (pdfRenderer != null)
			{
				pdfRenderer.Filename = this.m_filename;
			}
			ApplyScrollInfo();
		}

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			if ((e.Property == PdfArea.FontFamilyProperty)
			    || (e.Property == PdfArea.FontSizeProperty)
			   )
			{
//			    || (e.Property == PdfArea.FontStretchProperty)
//			    || (e.Property == PdfArea.FontStyleProperty)
//			    || (e.Property == PdfArea.FontWeightProperty)
				MeasureFont();
			}

			base.OnPropertyChanged(e);
		}

		void MeasureFont()
		{
			Typeface tf = new Typeface(
				this.FontFamily,
				FontStyles.Normal,
				FontWeights.Normal,
				FontStretches.Normal
			);// this.GetTypeface();
			
			FormattedText fText = new FormattedText(new string('8', 10),
			                                        CultureInfo.CurrentCulture,
			                                        FlowDirection.LeftToRight,
			                                        tf,
			                                        FontSize, Foreground
			                                       );

//			_charWidth = fText.Width / 10;
			this.ColumnWidth = fText.Width *3.0d / 10.0d;
			this.LineHeight = fText.Height;

//			if (pdfRenderer != null)
//			{
//				_linesInpage = (int)Math.Round(pdfRenderer.RenderSize.Height / _lineHeight, 0);
//			}
//			else
//			{
//				_linesInpage = 0;
//			}
		}


		ScrollViewer scrollOwner;
		bool canVerticallyScroll, canHorizontallyScroll;

		void ApplyScrollInfo()
		{
			if (pdfRenderer != null)
			{
				pdfRenderer.ScrollOwner = scrollOwner;
				(pdfRenderer as IScrollInfo).CanVerticallyScroll = canVerticallyScroll;
				(pdfRenderer as IScrollInfo).CanHorizontallyScroll = canHorizontallyScroll;
				scrollOwner = null;
			}
		}

		bool IScrollInfo.CanVerticallyScroll
		{
			get { return pdfRenderer != null ? (pdfRenderer as IScrollInfo).CanVerticallyScroll : false; }
			set
			{
				canVerticallyScroll = value;
				if (pdfRenderer != null)
					(pdfRenderer as IScrollInfo).CanVerticallyScroll = value;
			}
		}

		bool IScrollInfo.CanHorizontallyScroll
		{
			get { return pdfRenderer != null ? (pdfRenderer as IScrollInfo).CanHorizontallyScroll : false; }
			set
			{
				canHorizontallyScroll = value;
				if (pdfRenderer != null)
					(pdfRenderer as IScrollInfo).CanHorizontallyScroll = value;
			}
		}

		double IScrollInfo.ExtentWidth
		{
			get
			{
				if (pdfRenderer == null)
					return 0.0d;
				double r = pdfRenderer.ExtentWidth;
				return r;
			}
		}

		double IScrollInfo.ExtentHeight
		{
			get
			{
				if (pdfRenderer == null)
					return 0.0d;
				double r = pdfRenderer.ExtentHeight;
					r += this.ColumnHeight;
				
				return r;
			}
		}

		double IScrollInfo.ViewportWidth
		{
			get
			{

				if (pdfRenderer == null)
					return 0.0d;
				double r = pdfRenderer.ViewportWidth;
				return r;
			}
		}

		double IScrollInfo.ViewportHeight
		{
			get
			{

				if (pdfRenderer == null)
					return 0.0d;
				double r = pdfRenderer.ViewportHeight;				
					r += this.ColumnHeight;
				
				return r;
			}
		}

		double IScrollInfo.HorizontalOffset
		{
			get { return pdfRenderer != null ? pdfRenderer.HorizontalOffset : 0; }
		}

		double IScrollInfo.VerticalOffset
		{
			get { return pdfRenderer != null ? pdfRenderer.VerticalOffset : 0; }
		}

		ScrollViewer IScrollInfo.ScrollOwner
		{
			get { return pdfRenderer != null ? pdfRenderer.ScrollOwner : null; }
			set
			{
				if (pdfRenderer != null)
					pdfRenderer.ScrollOwner = value;
				else
					scrollOwner = value;
			}
		}

		void IScrollInfo.LineUp()
		{
			if (pdfRenderer != null) pdfRenderer.LineUp();

		}

		void IScrollInfo.LineDown()
		{
			if (pdfRenderer != null) pdfRenderer.LineDown();
		}

		void IScrollInfo.LineLeft()
		{
			if (pdfRenderer != null)
				(pdfRenderer as IScrollInfo).LineLeft();
		}

		void IScrollInfo.LineRight()
		{
			if (pdfRenderer != null)
				(pdfRenderer as IScrollInfo).LineRight();
		}

		void IScrollInfo.PageUp()
		{
			if (pdfRenderer != null) pdfRenderer.PageUp();
		}

		void IScrollInfo.PageDown()
		{
			if (pdfRenderer != null) pdfRenderer.PageDown();
		}

		void IScrollInfo.PageLeft()
		{
			if (pdfRenderer != null) pdfRenderer.PageLeft();
		}

		void IScrollInfo.PageRight()
		{
			if (pdfRenderer != null) pdfRenderer.PageRight();
		}

		void IScrollInfo.MouseWheelUp()
		{
			if (pdfRenderer != null) (pdfRenderer as IScrollInfo).MouseWheelUp();
		}

		void IScrollInfo.MouseWheelDown()
		{
			if (pdfRenderer != null) (pdfRenderer as IScrollInfo).MouseWheelDown();
		}

		void IScrollInfo.MouseWheelLeft()
		{
			if (pdfRenderer != null) (pdfRenderer as IScrollInfo).MouseWheelLeft();
		}

		void IScrollInfo.MouseWheelRight()
		{
			if (pdfRenderer != null) (pdfRenderer as IScrollInfo).MouseWheelRight();
		}

		void IScrollInfo.SetHorizontalOffset(double offset)
		{
			if (pdfRenderer != null) (pdfRenderer as IScrollInfo).SetHorizontalOffset(offset);
		}

		void IScrollInfo.SetVerticalOffset(double offset)
		{
			if (pdfRenderer != null) (pdfRenderer as IScrollInfo).SetVerticalOffset(offset);
		}

		Rect IScrollInfo.MakeVisible(System.Windows.Media.Visual visual, Rect rectangle)
		{
			if (pdfRenderer != null)
				return pdfRenderer.MakeVisible(visual, rectangle);
			else
				return Rect.Empty;
		}
	}

}
