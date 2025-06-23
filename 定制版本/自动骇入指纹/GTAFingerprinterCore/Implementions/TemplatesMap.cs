using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;

namespace GTAFingerprinterCore.Implementions
{
    public abstract class TemplatesMap : ReadOnlyDictionary<Bitmap, IList<Bitmap>>, IReadOnlyDictionary<Bitmap, IList<Bitmap>>
    {
        public TemplatesMap(IDictionary<Bitmap, IList<Bitmap>> dictionary) : base(dictionary)
        {
        }
    }
}
