﻿using System;
using System.Windows;

#pragma warning disable 1591

namespace PdfiumViewer
{
    public struct PdfPoint : IEquatable<PdfPoint>
    {
        public static readonly PdfPoint Empty = new PdfPoint();

        // _page is offset by 1 so that Empty returns an invalid point.
        private readonly int _page;

        public int Page
        {
            get { return _page - 1; }
        }

        public Point Location;

        public bool IsValid
        {
            get { return _page != 0; }
        }

        public PdfPoint(int page, Point location)
        {
            _page = page + 1;
            Location = location;
        }

        public bool Equals(PdfPoint other)
        {
            return
                Page == other.Page &&
                Location == other.Location;
        }

        public override bool Equals(object obj)
        {
            return
                obj is PdfPoint &&
                Equals((PdfPoint)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Page * 397) ^ Location.GetHashCode();
            }
        }

        public static bool operator ==(PdfPoint left, PdfPoint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PdfPoint left, PdfPoint right)
        {
            return !left.Equals(right);
        }
    }
}
