using System.Drawing;

namespace GTAFingerprinterCore.Interfaces
{
    public interface IScreenCapture
    {
        public Bitmap CaptureWindow(string title);

        public Bitmap CaptureScreen();
    }
}