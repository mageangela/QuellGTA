using System.Drawing;

namespace GTAFingerprinterCore.Interfaces
{
    public interface IImageComparer
    {
        double Similarity(Image i1, Image i2);
    }
}