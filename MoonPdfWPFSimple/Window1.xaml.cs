/*
 * Created by SharpDevelop.
 * User: Administrator
 * Date: 2017-07-02
 * Time: 16:11
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
using MoonPdfLib.MuPdf;

namespace MoonPdfWPFSimple
{
	/// <summary>
	/// Interaction logic for Window1.xaml
	/// </summary>
	public partial class Window1 : Window
	{
		public Window1()
		{
			InitializeComponent();
		}
		
		void Window_Loaded(object sender, RoutedEventArgs e)
		{
			this.pdfViewer1.Open(new MemorySource(MoonPdfWPFSimple.Properties.Resources.glm));
		}
	}
}