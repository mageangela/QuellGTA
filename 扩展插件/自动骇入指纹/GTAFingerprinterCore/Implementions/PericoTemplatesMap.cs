using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace GTAFingerprinterCore.Implementions
{
    public class PericoTemplatesMap : TemplatesMap
    {
        public PericoTemplatesMap(IDictionary<Bitmap, IList<Bitmap>> dictionary) : base(dictionary)
        {
        }
    }
}
