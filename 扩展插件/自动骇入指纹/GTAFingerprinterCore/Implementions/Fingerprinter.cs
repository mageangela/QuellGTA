using GTAFingerprinterCore.Configurations;
using GTAFingerprinterCore.Extensions;
using GTAFingerprinterCore.Interfaces;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace GTAFingerprinterCore.Implementions
{
    public abstract class Fingerprinter : IFingerprinter
    {
        private readonly IScreenCapture _screenCapture;
        public const string GameWindowName = "Grand Theft Auto V";

        protected Fingerprinter(IScreenCapture screenCapture)
        {
            _screenCapture = screenCapture;
        }

        public async Task<Bitmap> CaptureGameScreenAsync(bool isFullScreen)
        {
            Bitmap bitmap;
            if (isFullScreen)
                bitmap = _screenCapture.CaptureScreen();
            else
                bitmap = _screenCapture.CaptureWindow(GameWindowName);
            return await ResizeAsync(bitmap);
        }

        public async Task<Bitmap> ResizeAsync(Bitmap img)
        {
            var ow = img.Width;
            var oh = img.Height;
            var nh = 720;
            var nw = (int)(ow / ((double)oh / 720));
            var resizeImg = img.Resize(nw, nh);
            if (nw != 1280)
            {
                return await resizeImg.SetBorderAsync((1280 - nw) / 2, BorderType.Left | BorderType.Right);
            }
            return resizeImg;
        }

        public abstract Task<Bitmap> CutBigAsync(Bitmap image, ImgConfig config);
        public abstract Task<IList<Bitmap>> CutSubsAsync(Bitmap image, ImgConfig config);
        public abstract Task<IList<int>> RecognizeAsync(Bitmap big, IList<Bitmap> subs, float similarity);
        public abstract Task AutoPressKeysAsync(IList<int> corrects, int delay);
    }
}
