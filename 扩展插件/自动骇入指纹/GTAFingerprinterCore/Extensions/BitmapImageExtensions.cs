using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace GTAFingerprinterCore.Extensions
{
    public static class BitmapImageExtensions
    {
        public static Bitmap ToBitmap(this BitmapImage bitmapImage)
        {
            using var outStream = new MemoryStream();
            var enc = new BmpBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bitmapImage));
            enc.Save(outStream);
            var bitmap = new Bitmap(outStream);
            return new Bitmap(bitmap);
        }
    }
}