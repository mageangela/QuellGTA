using GTAFingerprinterCore.Configurations;
using GTAFingerprinterCore.Extensions;
using GTAFingerprinterCore.Interfaces;
using GTAFingerprinterCore.Shared;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace GTAFingerprinterCore.Implementions
{
    public class DiamondFingerprinter : Fingerprinter, IDiamondFingerprinter
    {
        private readonly DiamondTemplatesMap _templatesMap;
        private readonly IImageComparer _imageComparer;
        private readonly IKeyboard _keyboard;
        public DiamondFingerprinter(DiamondTemplatesMap templatesMap, IImageComparer imageComparer, IKeyboard keyboard, IScreenCapture screenCapture) : base(screenCapture)
        {
            _templatesMap = templatesMap;
            _imageComparer = imageComparer;
            _keyboard = keyboard;
        }

        public override async Task AutoPressKeysAsync(IList<int> corrects, int delay)
        {
            int tmpX = 0;
            int tmpY = 0;
            if (corrects == null) return;
            foreach (var item in corrects)
            {
                var x = item % 2;
                var y = item / 2;
                var deltaX = x - tmpX;
                var deltaY = y - tmpY;

                var xPressCount = Math.Abs(deltaX);
                var yPressCount = Math.Abs(deltaY);
                await _keyboard.Press(deltaX >= 0 ? Keys.Right : Keys.Left, xPressCount, delay: delay);
                await _keyboard.Press(deltaY >= 0 ? Keys.Down : Keys.Up, yPressCount, delay: delay);
                await _keyboard.Press(Keys.Enter, delay: delay);

                tmpX = x;
                tmpY = y;
            }
            await _keyboard.Press(Keys.Tab, delay: delay);
        }


        public override async Task<Bitmap> CutBigAsync(Bitmap image, ImgConfig config)
        {
            return await image.CutAsync(config.X, config.Y, config.X + config.Width, config.Y + config.Height);
        }

        public override async Task<IList<Bitmap>> CutSubsAsync(Bitmap image, ImgConfig config)
        {
            var subs = new List<Bitmap>();
            for (int i = 0; i < 8; i++)
            {
                var row = i / 2;
                var col = i % 2;
                var x1 = config.X + col * (config.Width + config.Space);
                var y1 = config.Y + row * (config.Width + config.Space);
                var x2 = x1 + config.Width;
                var y2 = y1 + config.Width;
                subs.Add(await image.CutAsync(x1, y1, x2, y2));
            }
            return subs;
        }

        public override async Task<IList<int>> RecognizeAsync(Bitmap main, IList<Bitmap> subs, float similarity)
        {
            var max = 0d;
            Bitmap cBig = null;
            foreach (var cKey in _templatesMap.Keys)
            {
                var s = _imageComparer.Similarity(main, cKey);
                if (s >= similarity && s > max)
                {
                    max = s;
                    cBig = cKey;
                }
            }
            if (cBig == null)
            {
                return null;
            }
            var corrects = new List<int>();
            await Task.Run(() =>
            {
                foreach (var sub in subs)
                {
                    foreach (var cSub in _templatesMap[cBig])
                    {
                        if (_imageComparer.Similarity(sub, cSub) >= similarity)
                        {
                            corrects.Add(subs.IndexOf(sub));
                            break;
                        }
                    }
                    if (corrects.Count == 4)
                    {
                        break;
                    }
                }
            });
            return corrects;
        }
    }
}