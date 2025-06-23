using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace GTAFingerprinterCore.Extensions
{
    public static class BitmapExtensions
    {
        /// <summary>
        ///  裁剪图片
        /// </summary>
        /// <param name="image">被裁剪的图片</param>
        /// <param name="x1">裁剪区域左上角x坐标</param>
        /// <param name="y1">裁剪区域左上角y坐标</param>
        /// <param name="x2">裁剪区域右下角x坐标</param>
        /// <param name="y2">裁剪区域右下角y坐标</param>
        /// <returns>裁剪后的图片</returns>
        public static async Task<Bitmap> CutAsync(this Bitmap image, int x1, int y1, int x2, int y2)
        {
            int w = x2 - x1;
            int h = y2 - y1;
            var srcRec = new Rectangle(x1, y1, w, h);
            var dstRec = new Rectangle(0, 0, w, h);
            var dst = new Bitmap(w, h);
            using (var src = new Bitmap(image))
            using (var g = Graphics.FromImage(dst))
            {
                await Task.Run(() => g.DrawImage(src, dstRec, srcRec, GraphicsUnit.Pixel));
            }
            dst = dst.PreProcess(3);
            return dst;
        }

        /// <summary>
        /// 裁剪图片
        /// </summary>
        /// <param name="image">被裁剪的图片</param>
        /// <param name="start">裁剪区域左上角坐标</param>
        /// <param name="end">裁剪区域右下角坐标</param>
        /// <returns>裁剪后的图片</returns>
        public static async Task<Bitmap> CutAsync(this Bitmap image, Point start, Point end)
        {
            return await CutAsync(image, start.X, start.Y, end.X, end.Y);
        }

        /// <summary>
        /// 图片预处理，给图片加上黑色边框
        /// </summary>
        /// <param name="image">原图</param>
        /// <param name="n">边框宽度</param>
        /// <returns>加上边框后的图</returns>
        public static Bitmap PreProcess(this Bitmap image, int n)
        {
            var b = new Bitmap(image);
            for (int i = 0; i < b.Width; i++)
            {
                for (int j = 0; j < b.Height; j++)
                {
                    if (i < n || i >= b.Width - n || j < n || j >= b.Height - n)
                    {
                        b.SetPixel(i, j, Color.Black);
                    }
                }
            }
            image.Dispose();
            return b;
        }

        /// <summary>
        /// 对图片进行缩放,原图会被Dispose
        /// </summary>
        /// <param name="image">原图</param>
        /// <param name="dstWidth">目标宽度</param>
        /// <param name="dstHeight">目标高度</param>
        /// <returns>缩放后的图片</returns>
        public static Bitmap Resize(this Bitmap image, int dstWidth, int dstHeight)
        {
            Bitmap resize = new Bitmap(dstWidth, dstHeight);
            using var g = Graphics.FromImage(resize);
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(image, 0, 0, dstWidth, dstHeight);
            image.Dispose();
            return resize;
        }

        /// <summary>
        /// 将图片裁剪或增大，根据边框宽度参数，原图会被Dispose
        /// </summary>
        /// <param name="image"></param>
        /// <param name="borderWidth"></param>
        /// <param name="type"></param>
        public static async Task<Bitmap> SetBorderAsync(this Bitmap image, int borderWidth, BorderType type)
        {
            int top = 0, right = 0, bottom = 0, left = 0;
            if ((type & BorderType.Top) == BorderType.Top)
            {
                top = borderWidth;
            }
            if ((type & BorderType.Right) == BorderType.Right)
            {
                right = borderWidth;
            }
            if ((type & BorderType.Bottom) == BorderType.Bottom)
            {
                bottom = borderWidth;
            }
            if ((type & BorderType.Left) == BorderType.Left)
            {
                left = borderWidth;
            }
            if (borderWidth > 0)
            {
                var nw = image.Width + right + left;
                var nh = image.Height + top + bottom;
                Bitmap nImg = new Bitmap(nw, nh);
                using var g = Graphics.FromImage(nImg);
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(image, new Rectangle(left, top, image.Width, image.Height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
                image.Dispose();
                return nImg;
            }
            else
            {
                var nImg = await image.CutAsync(-left, -top, image.Width + right, image.Height + bottom);
                image.Dispose();
                return nImg;
            }
        }

        public static BitmapImage ToBitmapImage(this Bitmap bitmap)
        {
            BitmapImage bitmapImage = new BitmapImage();
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Png);
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = ms;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }
            return bitmapImage;
        }
    }

    public enum BorderType
    {
        Top = 1,
        Right = 2,
        Bottom = 4,
        Left = 8
    }
}