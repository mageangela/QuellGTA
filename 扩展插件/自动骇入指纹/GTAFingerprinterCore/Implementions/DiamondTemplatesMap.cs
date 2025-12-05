using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace GTAFingerprinterCore.Implementions
{
    public class DiamondTemplatesMap : TemplatesMap
    {
        public DiamondTemplatesMap(IDictionary<Bitmap, IList<Bitmap>> dictionary) : base(dictionary)
        {
        }
    }
}
