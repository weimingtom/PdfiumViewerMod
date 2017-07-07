/*
 * Created by SharpDevelop.
 * User: Administrator
 * Date: 2017-06-20
 * Time: 19:45
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using PdfiumViewer;

namespace PdfiumViewerWinFormSimple
{
	/// <summary>
	/// Description of MainForm.
	/// </summary>
	public partial class MainForm : Form
	{
		public MainForm()
		{
			//
			// The InitializeComponent() call is required for Windows Forms designer support.
			//
			InitializeComponent();
			
			//
			// TODO: Add constructor code after the InitializeComponent() call.
			//
		}
		
		void MainFormLoad(object sender, EventArgs e)
		{
			string message = "";
            pdfViewer1.Document = OpenDocument2(new MemoryStream(PdfiumViewerWinFormSimple.Properties.Resources.glm), ref message);
        }

        private PdfDocument OpenDocument2(Stream LoadStream, ref string message)
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
	}
}
