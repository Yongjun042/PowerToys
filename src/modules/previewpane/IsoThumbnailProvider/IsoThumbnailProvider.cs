using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using Common.ComInterlop;
using Common.Cominterop;
using Common.Utilities;
using PreviewHandlerCommon;
using DiscUtils;
using DiscUtils.Udf;

namespace Microsoft.PowerToys.ThumbnailHandler.Iso
{
    /// <summary>
    /// ISO Thumbnail Provider.
    /// </summary>
    [Guid("F7063625-8A36-4F01-94BE-BC9E80D8CFA7")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class IsoThumbnailProvider : IInitializeWithFile, IThumbnailProvider
    {
        /// <summary>
        /// Gets the file path to access file.
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        ///  The maximum dimension (width or height) thumbnail we will generate.
        /// </summary>
        private const uint MaxThumbnailSize = 10000;

        /// <summary>
        /// Resize the image with high quality to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            if (width <= 0 ||
                height <= 0 ||
                width > MaxThumbnailSize ||
                height > MaxThumbnailSize ||
                image == null)
            {
                return null;
            }

            Bitmap destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.Clear(Color.White);
                graphics.DrawImage(image, 0, 0, width, height);
            }

            return destImage;
        }

        public void Initialize([MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, uint grfMode)
        {
            // Ignore the grfMode always use read mode to access the file.
            this.FilePath = pszFilePath;
        }

        public void GetThumbnail(uint cx, out IntPtr phbmp, out WTS_ALPHATYPE pdwAlpha)
        {
            phbmp = IntPtr.Zero;
            pdwAlpha = WTS_ALPHATYPE.WTSAT_UNKNOWN;

            if (cx == 0 || cx > MaxThumbnailSize)
            {
                return;
            }

            Bitmap thumbnail = null;
            using (FileStream isoStream = File.OpenRead(this.FilePath))
            {
                UdfReader cd = new UdfReader(isoStream);
                //read META\DL img
                if (cd.Exists(@"BDMV\META\DL"))
                {
                    string[] files = cd.GetFiles(@"BDMV\META\DL", "*.jpg");
                    //find file that size is most similar and bigger than cx;
                    foreach (var item in files)
                    {
                        Stream fileStream = cd.OpenFile(item, FileMode.Open);
                        Bitmap bmp = new Bitmap(fileStream);
                        if (thumbnail != null)
                        {
                            if ((cx < Math.Max(thumbnail.Width, thumbnail.Height) && Math.Max(bmp.Width, bmp.Height) < Math.Max(thumbnail.Width, thumbnail.Height))
                                || (cx > Math.Max(thumbnail.Width, thumbnail.Height) && Math.Max(bmp.Width, bmp.Height) > Math.Max(thumbnail.Width, thumbnail.Height)))
                            {
                                thumbnail = bmp;
                            }
                        }
                    }
                    if (thumbnail == null)
                    {
                        return;
                    }
                    if (thumbnail.Width != cx && thumbnail.Height != cx)
                    {
                        // We are not the appropriate size for caller.  Resize now while
                        // respecting the aspect ratio.
                        float scale = Math.Min((float)cx / thumbnail.Width, (float)cx / thumbnail.Height);
                        int scaleWidth = (int)(thumbnail.Width * scale);
                        int scaleHeight = (int)(thumbnail.Height * scale);
                        thumbnail = ResizeImage(thumbnail, scaleWidth, scaleHeight);
                    }
                    phbmp = thumbnail.GetHbitmap();
                    pdwAlpha = WTS_ALPHATYPE.WTSAT_RGB;
                }
            }
        }

    }
}
