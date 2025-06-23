using GTAFingerprinterCore.Configurations;
using GTAFingerprinterCore.Extensions;
using GTAFingerprinterCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTAFingerprinterCore.Implementions
{
    public class PericoFingerprinter : Fingerprinter, IPericoFingerprinter
    {
        private readonly PericoTemplatesMap _templatesMap;
        private readonly IImageComparer _imageComparer;
        private readonly IKeyboard _keyboard;
        public PericoFingerprinter(PericoTemplatesMap templatesMap, IScreenCapture screenCapture, IImageComparer imageComparer, IKeyboard keyboard) : base(screenCapture)
        {
            _templatesMap = templatesMap;
            _imageComparer = imageComparer;
            _keyboard = keyboard;
        }

        public override async Task AutoPressKeysAsync(IList<int> corrects, int delay)
        {
            for (int i = 0; i < corrects.Count; i++)
            {
                var atRight = corrects[i] >= i;
                var distance = Math.Abs(corrects[i] - i);
                if (distance >= 4)
                {
                    if (atRight)
                    {
                        await _keyboard.Press(Shared.Keys.Right, 8 - distance);
                    }
                    else
                    {
                        await _keyboard.Press(Shared.Keys.Left, 8 - distance);

                    }
                }
                else if (distance > 0)
                {
                    if (atRight)
                    {
                        await _keyboard.Press(Shared.Keys.Left, distance);
                    }
                    else
                    {
                        await _keyboard.Press(Shared.Keys.Right, distance);

                    }
                }
                await _keyboard.Press(Shared.Keys.Down);
            }
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
                var x1 = config.X;
                var y1 = config.Y + i * (config.Height + config.Space);
                var x2 = x1 + config.Width;
                var y2 = y1 + config.Height;
                subs.Add(await image.CutAsync(x1, y1, x2, y2));
            }
            return subs;
        }

        public override async Task<IList<int>> RecognizeAsync(Bitmap big, IList<Bitmap> subs, float similarity)
        {
            var max = 0d;
            Bitmap cBig = null;
            foreach (var cKey in _templatesMap.Keys)
            {
                var s = _imageComparer.Similarity(big, cKey);
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
                var find = true;
                while (find)
                {
                    if (similarity < 0.5)
                    {
                        corrects = null;
                        return;
                    }
                    foreach (var sub in subs)
                    {
                        foreach (var cSub in _templatesMap[cBig])
                        {
                            if (_imageComparer.Similarity(sub, cSub) >= similarity)
                            {
                                corrects.Add(_templatesMap[cBig].IndexOf(cSub));
                                break;
                            }
                        }
                        if (corrects.Count == 8)
                        {
                            find = false;
                            break;
                        }
                    }
                    if (corrects.Count < 8)
                    {
                        similarity -= 0.1f;
                        corrects.Clear();
                    }
                }

            });
            return corrects;
        }
    }
}
