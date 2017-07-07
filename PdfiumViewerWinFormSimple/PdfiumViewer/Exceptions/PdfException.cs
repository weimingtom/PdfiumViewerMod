using System;

#pragma warning disable 1591

namespace PdfiumViewer
{
    public class PdfException : Exception
    {
        public PdfError Error;

        public PdfException(PdfError error)
            : this(GetMessage(error))
        {
            Error = error;
        }

        //FIXME:
        private static string GetMessage(PdfError error)
        {
            switch (error)
            {
                case PdfError.Success:
                    return "没有错误";
                case PdfError.CannotOpenFile:
                    return "文件未找到或无法打开";
                case PdfError.InvalidFormat:
                    return "文件不是PDF格式或损坏";
                case PdfError.PasswordProtected:
                    return "需要密码或不正确的密码";
                case PdfError.UnsupportedSecurityScheme:
                    return "不支持的安全方案";
                case PdfError.PageNotFound:
                    return "页面找不到或内容错误";
                default:
                    return "未知错误";
            }
        }

        public PdfException(string message)
            : base(message)
        {
        }
    }
}
