using System.ComponentModel;
using System.Windows.Forms;

namespace PdfiumViewer
{
    /// <summary>
    /// Control to host PDF documents with support for printing.
    /// </summary>
    public partial class PdfViewer : UserControl
    {
        private PdfDocument _document;
        private PdfRenderer _renderer;

        /// <summary>
        /// Gets or sets the PDF document.
        /// </summary>
        [DefaultValue(null)]
        public PdfDocument Document
        {
            get { return _document; }
            set
            {
                if (_document != value)
                {
                    _document = value;

                    if (_document != null)
                    {
                        _renderer.Load(_document);
                    }
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the PdfViewer class.
        /// </summary>
        public PdfViewer()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this._renderer = new PdfiumViewer.PdfRenderer();
            this.Controls.Add(this._renderer);
            this._renderer.Cursor = System.Windows.Forms.Cursors.Default;
            this._renderer.Name = "_renderer";
            this._renderer.Page = 0;
            this._renderer.ZoomMode = PdfiumViewer.PdfViewerZoomMode.FitHeight;
            this._renderer.Dock = DockStyle.Fill;
        }
    }
}
