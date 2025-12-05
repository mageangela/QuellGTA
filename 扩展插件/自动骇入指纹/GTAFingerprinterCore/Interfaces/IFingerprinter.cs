using GTAFingerprinterCore.Configurations;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;

namespace GTAFingerprinterCore.Interfaces
{
    public interface IFingerprinter
    {
        Task AutoPressKeysAsync(IList<int> corrects, int delay);
        Task<Bitmap> CaptureGameScreenAsync(bool isFullScreen);
        Task<Bitmap> CutBigAsync(Bitmap image, ImgConfig config);
        Task<IList<Bitmap>> CutSubsAsync(Bitmap image, ImgConfig config);
        Task<IList<int>> RecognizeAsync(Bitmap big, IList<Bitmap> subs, float similarity);
    }
}
