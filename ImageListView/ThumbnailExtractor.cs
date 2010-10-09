﻿// ImageListView - A listview control for image files
// Copyright (C) 2009 Ozgur Ozcitak
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// Ozgur Ozcitak (ozcitak@yahoo.com)
//
// WIC support coded by Jens

using System;
using System.IO;
using System.Drawing.Imaging;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
#if USEWIC
using System.Windows.Media;
using System.Windows.Media.Imaging;
#endif

namespace Manina.Windows.Forms
{
    /// <summary>
    /// Extracts thumbnails from images.
    /// </summary>
    internal static class ThumbnailExtractor
    {
        #region Exif Tag IDs
        /// <summary>
        /// Represents the Exif tag for thumbnail data.
        /// </summary>
        private const int TagThumbnailData = 0x501B;
        private const int TagOrientation = 0x0112;
        #endregion

        #region Public Methods
        /// <summary>
        /// Creates a thumbnail from the given image.
        /// </summary>
        /// <param name="image">The source image.</param>
        /// <param name="size">Requested image size.</param>
        /// <param name="useEmbeddedThumbnails">Embedded thumbnail usage.</param>
        /// <param name="useExifOrientation">true to automatically rotate images based on Exif orientation; otherwise false.</param>
        /// <param name="useWIC">true to use Windows Imaging Component; otherwise false.</param>
        /// <returns>The thumbnail image from the given image or null if an error occurs.</returns>
        public static Image FromImage(Image image, Size size, UseEmbeddedThumbnails useEmbeddedThumbnails, bool useExifOrientation, bool useWIC)
        {
            if (size.Width <= 0 || size.Height <= 0)
                throw new ArgumentException();

            if (useWIC)
            {
#if USEWIC
                MemoryStream stream = null;
                BitmapFrame frameWpf = null;
                try
                {
                    stream = new MemoryStream();

                    image.Save(stream, ImageFormat.MemoryBmp);
                    // Performance vs image quality settings.
                    // Selecting BitmapCacheOption.None speeds up thumbnail generation of large images tremendously
                    // if the file contains no embedded thumbnail. The image quality is only slightly worse.
                    frameWpf = BitmapFrame.Create(stream,
                        BitmapCreateOptions.IgnoreColorProfile,
                        BitmapCacheOption.None);
                }
                catch
                {
                    if (stream != null)
                    {
                        stream.Dispose();
                        stream = null;
                    }
                    frameWpf = null;
                }

                if (stream == null || frameWpf == null)
                {
                    if (stream != null)
                    {
                        stream.Dispose();
                        stream = null;
                    }

                    // .Net 2.0 fallback
                    Image img = GetThumbnailBmp(image, size,
                        useExifOrientation ? GetRotation(image) : 0);
                    return img;
                }

                Image thumb = GetThumbnail(frameWpf, size, useEmbeddedThumbnails,
                    useExifOrientation ? GetRotation(frameWpf) : 0);
                stream.Dispose();
                return thumb;
#else
            // .Net 2.0 fallback
            Image img = GetThumbnailBmp(image, size);
            return img;
#endif
            }
            else
            {
                // .Net 2.0 fallback
                Image img = GetThumbnailBmp(image, size,
                    useExifOrientation ? GetRotation(image) : 0);

                return img;
            }
        }
        /// <summary>
        /// Creates a thumbnail from the given image file.
        /// </summary>
        /// <comment>
        /// This much faster .NET 3.0 method replaces the original .NET 2.0 method.
        /// The image quality is slightly reduced (low filtering mode).
        /// </comment>
        /// <param name="filename">The filename pointing to an image.</param>
        /// <param name="size">Requested image size.</param>
        /// <param name="useEmbeddedThumbnails">Embedded thumbnail usage.</param>
        /// <param name="useExifOrientation">true to automatically rotate images based on Exif orientation; otherwise false.</param>
        /// <param name="useWIC">true to use Windows Imaging Component; otherwise false.</param>
        /// <returns>The thumbnail image from the given file or null if an error occurs.</returns>
        public static Image FromFile(string filename, Size size, UseEmbeddedThumbnails useEmbeddedThumbnails, bool useExifOrientation, bool useWIC)
        {
            if (string.IsNullOrEmpty(filename))
                throw new ArgumentException("Filename cannot be empty", "filename");

            if (size.Width <= 0 || size.Height <= 0)
                throw new ArgumentException("Thumbnail size cannot be empty.", "size");

            if (useWIC)
            {
#if USEWIC
                // File can be read and an image is recognized.
                FileStream stream = null;
                BitmapFrame frameWpf = null;
                try
                {
                    stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (stream != null)
                    {
                        // Performance vs image quality settings.
                        // Selecting BitmapCacheOption.None speeds up thumbnail generation of large images tremendously
                        // if the file contains no embedded thumbnail. The image quality is only slightly worse.
                        frameWpf = BitmapFrame.Create(stream,
                            BitmapCreateOptions.IgnoreColorProfile,
                            BitmapCacheOption.None);
                    }
                }
                catch
                {
                    if (stream != null)
                    {
                        stream.Dispose();
                        stream = null;
                    }
                    frameWpf = null;
                }

                if (stream == null || frameWpf == null)
                {
                    if (stream != null)
                    {
                        stream.Dispose();
                        stream = null;
                    }

                    // .Net 2.0 fallback
                    Image img = GetThumbnailBmp(filename, size, useEmbeddedThumbnails,
                        useExifOrientation ? GetRotation(filename) : 0);
                    return img;
                }

                Image thumb = GetThumbnail(frameWpf, size, useEmbeddedThumbnails,
                    useExifOrientation ? GetRotation(frameWpf) : 0);

                stream.Dispose();
                return thumb;
#else
            // .Net 2.0 fallback
            Image img = GetThumbnailBmp(filename, size, useEmbeddedThumbnails);
            return img;
#endif
            }
            else
            {
                // .Net 2.0 fallback
                Image img = GetThumbnailBmp(filename, size, useEmbeddedThumbnails,
                    useExifOrientation ? GetRotation(filename) : 0);
                return img;
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Creates a thumbnail from the given image.
        /// </summary>
        /// <param name="image">The source image.</param>
        /// <param name="size">Requested image size.</param>
        /// <param name="rotate">Rotation angle.</param>
        /// <returns>The image from the given file or null if an error occurs.</returns>
        internal static Image GetThumbnailBmp(Image image, Size size, int rotate)
        {
            if (size.Width <= 0 || size.Height <= 0)
                throw new ArgumentException();

            Image thumb = null;
            try
            {
                double scale;
                if (rotate % 180 != 0)
                {
                    scale = Math.Min(size.Height / (double)image.Width,
                        size.Width / (double)image.Height);
                }
                else
                {
                    scale = Math.Min(size.Width / (double)image.Width,
                        size.Height / (double)image.Height);
                }

                thumb = ScaleDownRotateBitmap(image, scale, rotate);
            }
            catch
            {
                if (thumb != null)
                    thumb.Dispose();
                thumb = null;
            }

            return thumb;
        }
        /// <summary>
        /// Creates a thumbnail from the given image file.
        /// </summary>
        /// <param name="filename">The filename pointing to an image.</param>
        /// <param name="size">Requested image size.</param>
        /// <param name="useEmbeddedThumbnails">Embedded thumbnail usage.</param>
        /// <param name="rotate">Rotation angle.</param>
        /// <returns>The image from the given file or null if an error occurs.</returns>
        internal static Image GetThumbnailBmp(string filename, Size size, UseEmbeddedThumbnails useEmbeddedThumbnails, int rotate)
        {
            if (size.Width <= 0 || size.Height <= 0)
                throw new ArgumentException();

            // Check if this is an image file
            try
            {
                using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    if (!Utility.IsImage(stream))
                        return null;
                }
            }
            catch
            {
                return null;
            }

            Image source = null;
            Image thumb = null;

            // Try to read the exif thumbnail
            if (useEmbeddedThumbnails != UseEmbeddedThumbnails.Never)
            {
                try
                {
                    using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                    {
                        using (Image img = Image.FromStream(stream, false, false))
                        {
                            foreach (int index in img.PropertyIdList)
                            {
                                if (index == TagThumbnailData)
                                {
                                    // Fetch the embedded thumbnail
                                    byte[] rawImage = img.GetPropertyItem(TagThumbnailData).Value;
                                    using (MemoryStream memStream = new MemoryStream(rawImage))
                                    {
                                        source = Image.FromStream(memStream);
                                    }
                                    if (useEmbeddedThumbnails == UseEmbeddedThumbnails.Auto)
                                    {
                                        // Check that the embedded thumbnail is large enough.
                                        if (Math.Max((float)source.Width / (float)size.Width,
                                            (float)source.Height / (float)size.Height) < 1.0f)
                                        {
                                            source.Dispose();
                                            source = null;
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    if (source != null)
                        source.Dispose();
                    source = null;
                }
            }

            // Fix for the missing semicolon in GIF files
            MemoryStream streamCopy = null;
            try
            {
                if (source == null)
                {
                    using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                    {
                        byte[] gifSignature = new byte[4];
                        stream.Read(gifSignature, 0, 4);
                        if (Encoding.ASCII.GetString(gifSignature) == "GIF8")
                        {
                            stream.Seek(0, SeekOrigin.Begin);
                            streamCopy = new MemoryStream();
                            byte[] buffer = new byte[32768];
                            int read = 0;
                            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                streamCopy.Write(buffer, 0, read);
                            }
                            // Append the missing semicolon
                            streamCopy.Seek(-1, SeekOrigin.End);
                            if (streamCopy.ReadByte() != 0x3b)
                                streamCopy.WriteByte(0x3b);
                            source = Image.FromStream(streamCopy);
                        }
                    }
                }
            }
            catch
            {
                if (source != null)
                    source.Dispose();
                source = null;
                if (streamCopy != null)
                    streamCopy.Dispose();
                streamCopy = null;
            }

            // Revert to source image if an embedded thumbnail of required size
            // was not found.
            FileStream sourceStream = null;
            if (source == null)
            {
                try
                {
                    sourceStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
                    source = Image.FromStream(sourceStream);
                }
                catch
                {
                    if (source != null)
                        source.Dispose();
                    if (sourceStream != null)
                        sourceStream.Dispose();
                    source = null;
                    sourceStream = null;
                }
            }

            // If all failed, return null.
            if (source == null) return null;

            // Create the thumbnail
            try
            {
                double scale;
                if (rotate % 180 != 0)
                {
                    scale = Math.Min(size.Height / (double)source.Width,
                        size.Width / (double)source.Height);
                }
                else
                {
                    scale = Math.Min(size.Width / (double)source.Width,
                        size.Height / (double)source.Height);
                }

                thumb = ScaleDownRotateBitmap(source, scale, rotate);
            }
            catch
            {
                if (thumb != null)
                    thumb.Dispose();
                thumb = null;
            }
            finally
            {
                if (source != null)
                    source.Dispose();
                source = null;
                if (sourceStream != null)
                    sourceStream.Dispose();
                sourceStream = null;
                if (streamCopy != null)
                    streamCopy.Dispose();
                streamCopy = null;
            }

            return thumb;
        }
#if USEWIC
        /// <summary>
        /// Returns Exif rotation in degrees. Returns 0 if the metadata 
        /// does not exist or could not be read. A negative value means
        /// the image needs to be mirrored about the vertical axis.
        /// </summary>
        /// <param name="frameWpf">Image source.</param>
        private static int GetRotation(BitmapFrame frameWpf)
        {
            BitmapMetadata data = frameWpf.Metadata as BitmapMetadata;
            if (data != null)
            {
                try
                {
                    // Look at Exif IFD
                    object obj = data.GetQuery("/app1/ifd/{ushort=274}");
                    // Try the XMP metadata
                    if (obj == null)
                        obj = data.GetQuery("/xmp/tiff:Orientation");
                    if (obj == null)
                        return 0;
                    ushort orientationFlag = (ushort)obj;
                    if (orientationFlag == 1)
                        return 0;
                    else if (orientationFlag == 2)
                        return -360;
                    else if (orientationFlag == 3)
                        return 180;
                    else if (orientationFlag == 4)
                        return -180;
                    else if (orientationFlag == 5)
                        return -90;
                    else if (orientationFlag == 6)
                        return 90;
                    else if (orientationFlag == 7)
                        return -270;
                    else if (orientationFlag == 8)
                        return 270;
                }
                catch
                {
                    ;
                }
            }

            return 0;
        }
        /// <summary>
        /// Creates a  thumbnail from the given bitmap.
        /// </summary>
        /// <param name="bmp">Source bitmap.</param>
        /// <param name="size">Requested image size.</param>
        /// <param name="useEmbeddedThumbnails">Embedded thumbnail usage.</param>
        /// <param name="rotate">Rotation angle in degrees.</param>
        private static Image GetThumbnail(BitmapFrame bmp, Size size, UseEmbeddedThumbnails useEmbeddedThumbnails, int rotate)
        {
            Image thumb = null;
            // Try to read the thumbnail.
            if (bmp.Thumbnail != null)
            {
                try
                {
                    BitmapSource sourceWpf = bmp.Thumbnail;
                    double scale;
                    if (rotate % 180 != 0)
                    {
                        scale = Math.Min(size.Height / (double)sourceWpf.PixelWidth,
                            size.Width / (double)sourceWpf.PixelHeight);
                    }
                    else
                    {
                        scale = Math.Min(size.Width / (double)sourceWpf.PixelWidth,
                            size.Height / (double)sourceWpf.PixelHeight);
                    }
                    if (bmp.Decoder == null ||
                        (bmp.Decoder.Preview == null && bmp.Decoder.Frames == null) ||
                        useEmbeddedThumbnails == UseEmbeddedThumbnails.Always)
                    {
                        // Take the thumbnail if nothing else is available or if ALWAYS
                        thumb = ConvertToBitmap(ScaleDownRotateBitmap(sourceWpf, scale, rotate));
                    }
                    else if (useEmbeddedThumbnails == UseEmbeddedThumbnails.Auto)
                    {
                        // Check that the embedded thumbnail is large enough.
                        if ((float)scale <= 1.0f)
                        {
                            thumb = ConvertToBitmap(ScaleDownRotateBitmap(sourceWpf, scale, rotate));
                        }
                    }
                }
                catch
                {
                    if (thumb != null)
                    {
                        thumb.Dispose();
                        thumb = null;
                    }
                }
            }

            // Try to read the preview.
            if (bmp.Decoder != null &&
                bmp.Decoder.Preview != null &&
                thumb == null)
            {
                try
                {
                    BitmapSource sourceWpf = bmp.Decoder.Preview;
                    double scale;
                    if (rotate % 180 != 0)
                    {
                        scale = Math.Min(size.Height / (double)sourceWpf.PixelWidth,
                            size.Width / (double)sourceWpf.PixelHeight);
                    }
                    else
                    {
                        scale = Math.Min(size.Width / (double)sourceWpf.PixelWidth,
                            size.Height / (double)sourceWpf.PixelHeight);
                    }
                    if (bmp.Decoder.Frames == null ||
                        useEmbeddedThumbnails == UseEmbeddedThumbnails.Always)
                    {
                        // Take the thumbnail if nothing else is available or if ALWAYS
                        thumb = ConvertToBitmap(ScaleDownRotateBitmap(sourceWpf, scale, rotate));
                    }
                    else if (useEmbeddedThumbnails == UseEmbeddedThumbnails.Auto)
                    {
                        // Check that the embedded thumbnail is large enough.
                        if ((float)scale <= 1.0f)
                        {
                            thumb = ConvertToBitmap(ScaleDownRotateBitmap(sourceWpf, scale, rotate));
                        }
                    }
                }
                catch
                {
                    if (thumb != null)
                    {
                        thumb.Dispose();
                        thumb = null;
                    }
                }
            }

            // Use source image if nothings else fits.
            if (bmp.Decoder != null &&
                bmp.Decoder.Frames != null &&
                thumb == null)
            {
                try
                {
                    BitmapSource sourceWpf = bmp.Decoder.Frames[0];
                    double scale;
                    if (rotate % 180 != 0)
                    {
                        scale = Math.Min(size.Height / (double)sourceWpf.PixelWidth,
                            size.Width / (double)sourceWpf.PixelHeight);
                    }
                    else
                    {
                        scale = Math.Min(size.Width / (double)sourceWpf.PixelWidth,
                            size.Height / (double)sourceWpf.PixelHeight);
                    }
                    thumb = ConvertToBitmap(ScaleDownRotateBitmap(sourceWpf, scale, rotate));
                }
                catch
                {
                    if (thumb != null)
                    {
                        thumb.Dispose();
                        thumb = null;
                    }
                }
            }

            return thumb;
        }

        /// <summary>
        /// Scales down and rotates a Wpf bitmap.
        /// </summary>
        /// <param name="sourceWpf">Original Wpf bitmap</param>
        /// <param name="scale">Uniform scaling factor</param>
        /// <param name="angle">Rotation angle</param>
        /// <returns>Scaled and rotated Wpf bitmap</returns>
        private static BitmapSource ScaleDownRotateBitmap(BitmapSource sourceWpf, double scale, int angle)
        {
            if (angle % 90 != 0)
            {
                throw new ArgumentException("Rotation angle should be a multiple of 90 degrees.", "angle");
            }

            // Do not upscale and no rotation.
            if ((float)scale >= 1.0f && angle == 0)
            {
                return sourceWpf;
            }

            // Set up the transformed thumbnail
            TransformedBitmap thumbWpf = new TransformedBitmap();
            thumbWpf.BeginInit();
            thumbWpf.Source = sourceWpf;
            TransformGroup transform = new TransformGroup();

            // Rotation
            if (Math.Abs(angle) % 360 != 0)
                transform.Children.Add(new RotateTransform(Math.Abs(angle)));

            // Scale
            if ((float)scale < 1.0f || angle < 0) // Only downscale
            {
                double xScale = Math.Min(1.0, Math.Max(1.0 / (double)sourceWpf.PixelWidth, scale));
                double yScale = Math.Min(1.0, Math.Max(1.0 / (double)sourceWpf.PixelHeight, scale));

                if (angle < 0)
                    xScale = -xScale;
                transform.Children.Add(new ScaleTransform(xScale, yScale));
            }

            // Apply the tranformation
            thumbWpf.Transform = transform;
            thumbWpf.EndInit();

            return thumbWpf;
        }

        /// <summary>
        /// Converts BitmapSource to Bitmap.
        /// </summary>
        /// <param name="sourceWpf">BitmapSource</param>
        /// <returns>Bitmap</returns>
        private static Bitmap ConvertToBitmap(BitmapSource sourceWpf)
        {
            BitmapSource bmpWpf = sourceWpf;

            // PixelFormat settings/conversion
            System.Drawing.Imaging.PixelFormat formatBmp = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
            if (sourceWpf.Format == PixelFormats.Bgr24)
            {
                formatBmp = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
            }
            else if (sourceWpf.Format == System.Windows.Media.PixelFormats.Pbgra32)
            {
                formatBmp = System.Drawing.Imaging.PixelFormat.Format32bppPArgb;
            }
            else if (sourceWpf.Format != System.Windows.Media.PixelFormats.Bgra32 &&
                     sourceWpf.Format != System.Windows.Media.PixelFormats.Bgr32)
            {
                // Convert BitmapSource
                FormatConvertedBitmap convertWpf = new FormatConvertedBitmap();
                convertWpf.BeginInit();
                convertWpf.Source = sourceWpf;
                convertWpf.DestinationFormat = PixelFormats.Bgra32;
                convertWpf.EndInit();
                bmpWpf = convertWpf;
            }

            // Copy/Convert to Bitmap
            Bitmap bmp = new Bitmap(bmpWpf.PixelWidth, bmpWpf.PixelHeight, formatBmp);
            Rectangle rect = new Rectangle(Point.Empty, bmp.Size);
            BitmapData data = bmp.LockBits(rect, ImageLockMode.WriteOnly, formatBmp);
            bmpWpf.CopyPixels(System.Windows.Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
            bmp.UnlockBits(data);

            return bmp;
        }
#endif
        /// <summary>
        /// Returns Exif rotation in degrees. Returns 0 if the metadata 
        /// does not exist or could not be read. A negative value means
        /// the image needs to be mirrored about the vertical axis.
        /// </summary>
        /// <param name="img">Image.</param>
        private static int GetRotation(Image img)
        {
            try
            {
                PropertyItem prop = img.GetPropertyItem(TagOrientation);
                ushort orientationFlag = BitConverter.ToUInt16(prop.Value, 0);
                if (orientationFlag == 1)
                    return 0;
                else if (orientationFlag == 2)
                    return -360;
                else if (orientationFlag == 3)
                    return 180;
                else if (orientationFlag == 4)
                    return -180;
                else if (orientationFlag == 5)
                    return -90;
                else if (orientationFlag == 6)
                    return 90;
                else if (orientationFlag == 7)
                    return -270;
                else if (orientationFlag == 8)
                    return 270;
            }
            catch
            {
                ;
            }

            return 0;
        }
        /// <summary>
        /// Returns Exif rotation in degrees. Returns 0 if the metadata 
        /// does not exist or could not be read. A negative value means
        /// the image needs to be mirrored about the vertical axis.
        /// </summary>
        /// <param name="filename">Image.</param>
        private static int GetRotation(string filename)
        {
            try
            {
                using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (Image img = Image.FromStream(stream, false, false))
                    {
                        return GetRotation(img);
                    }
                }
            }
            catch
            {
                ;
            }

            return 0;
        }

        /// <summary>
        /// Scales down and rotates an image.
        /// </summary>
        /// <param name="source">Original image</param>
        /// <param name="scale">Uniform scaling factor</param>
        /// <param name="angle">Rotation angle</param>
        /// <returns>Scaled and rotated image</returns>
        private static Image ScaleDownRotateBitmap(Image source, double scale, int angle)
        {
            if (angle % 90 != 0)
            {
                throw new ArgumentException("Rotation angle should be a multiple of 90 degrees.", "angle");
            }

            // Do not upscale and no rotation.
            if ((float)scale >= 1.0f && angle == 0)
            {
                return new Bitmap(source);
            }

            int sourceWidth = source.Width;
            int sourceHeight = source.Height;

            // Scale
            double xScale = Math.Min(1.0, Math.Max(1.0 / (double)sourceWidth, scale));
            double yScale = Math.Min(1.0, Math.Max(1.0 / (double)sourceHeight, scale));

            int width = (int)((double)sourceWidth * xScale);
            int height = (int)((double)sourceHeight * yScale);
            int thumbWidth = Math.Abs(angle) % 180 == 0 ? width : height;
            int thumbHeight = Math.Abs(angle) % 180 == 0 ? height : width;

            Image thumb = new Bitmap(thumbWidth, thumbHeight);
            using (Graphics g = Graphics.FromImage(thumb))
            {
                g.PixelOffsetMode = PixelOffsetMode.None;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(System.Drawing.Color.Transparent);

                g.TranslateTransform(-sourceWidth / 2, -sourceHeight / 2, MatrixOrder.Append);
                if (Math.Abs(angle) % 360 != 0)
                    g.RotateTransform(Math.Abs(angle), MatrixOrder.Append);
                if (angle < 0)
                    xScale = -xScale;
                g.ScaleTransform((float)xScale, (float)yScale, MatrixOrder.Append);
                g.TranslateTransform(thumbWidth / 2, thumbHeight / 2, MatrixOrder.Append);

                g.DrawImage(source, 0, 0);
            }

            return thumb;
        }
        #endregion
    }
}