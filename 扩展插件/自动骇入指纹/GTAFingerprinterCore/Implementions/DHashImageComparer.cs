using GTAFingerprinterCore.Interfaces;
using System;
using System.Text;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace GTAFingerprinterCore.Implementions
{
    public class DHashImageComparer : IImageComparer
    {
        /// <summary>
        /// 获取图片的D-hash值
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        private ulong GetHash(Image image)
        {
            int hashSize = 8;
            //图片缩小到9*8的尺寸
            using var thumbImage = Resize(image, hashSize + 1, hashSize);
            //获取灰度图片，灰度图片即把rgb转换成0~255的值
            using var grayImage = GetGrayScaleVersion(thumbImage);
            ulong hash = 0;

            //遍历9*8像素点，记录相邻像素之间的对边关系，产生8*8=64个对比关系，对应ulong的64位
            for (int x = 0; x < hashSize; x++)
            {
                for (int y = 0; y < hashSize; y++)
                {
                    //比较当前像素点与下一个像素点的对比关系，如果当前像素点值较大则为1，否则为0
                    var largerThanNext = Math.Abs(grayImage.GetPixel(y, x).R) > Math.Abs(grayImage.GetPixel(y + 1, x).R);
                    if (largerThanNext)
                    {
                        var currentIndex = x * hashSize + y;
                        hash |= (1UL << currentIndex);
                    }
                }
            }
            return hash;
        }

        /// <summary>
        /// 计算两个hash值之间的汉明距离
        /// </summary>
        /// <param name="hash1"></param>
        /// <param name="hash2"></param>
        /// <returns></returns>
        private double GetSimilarity(ulong hash1, ulong hash2)
        {
            return (64 - BitCount(hash1 ^ hash2)) / 64.0;
        }

        public double Similarity(Image i1, Image i2)
        {
            var u1 = GetHash(i1);
            var u2 = GetHash(i2);
            return GetSimilarity(u1, u2);
        }

        /// <summary>
        /// Bitcounts array used for BitCount method (used in Similarity comparisons).
        /// Don't try to read this or understand it, I certainly don't. Credit goes to
        /// David Oftedal of the University of Oslo, Norway for this.
        /// http://folk.uio.no/davidjo/computing.php
        /// </summary>
        private readonly byte[] bitCounts = {
            0,1,1,2,1,2,2,3,1,2,2,3,2,3,3,4,1,2,2,3,2,3,3,4,2,3,3,4,3,4,4,5,1,2,2,3,2,3,3,4,
            2,3,3,4,3,4,4,5,2,3,3,4,3,4,4,5,3,4,4,5,4,5,5,6,1,2,2,3,2,3,3,4,2,3,3,4,3,4,4,5,
            2,3,3,4,3,4,4,5,3,4,4,5,4,5,5,6,2,3,3,4,3,4,4,5,3,4,4,5,4,5,5,6,3,4,4,5,4,5,5,6,
            4,5,5,6,5,6,6,7,1,2,2,3,2,3,3,4,2,3,3,4,3,4,4,5,2,3,3,4,3,4,4,5,3,4,4,5,4,5,5,6,
            2,3,3,4,3,4,4,5,3,4,4,5,4,5,5,6,3,4,4,5,4,5,5,6,4,5,5,6,5,6,6,7,2,3,3,4,3,4,4,5,
            3,4,4,5,4,5,5,6,3,4,4,5,4,5,5,6,4,5,5,6,5,6,6,7,3,4,4,5,4,5,5,6,4,5,5,6,5,6,6,7,
            4,5,5,6,5,6,6,7,5,6,6,7,6,7,7,8
        };

        /// <summary>
        /// 计算ulong中位值为1的个数
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        private uint BitCount(ulong num)
        {
            uint count = 0;
            for (; num > 0; num >>= 8)
                count += bitCounts[(num & 0xff)];
            return count;
        }

        /// <summary>
        /// 修改图片尺寸
        /// </summary>
        /// <param name="originalImage"></param>
        /// <param name="newWidth"></param>
        /// <param name="newHeight"></param>
        /// <returns></returns>
        private Image Resize(Image originalImage, int newWidth, int newHeight)
        {
            Image smallVersion = new Bitmap(newWidth, newHeight);
            using (Graphics g = Graphics.FromImage(smallVersion))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(originalImage, 0, 0, newWidth, newHeight);
            }

            return smallVersion;
        }

        private readonly ColorMatrix ColorMatrix = new ColorMatrix(
          new float[][]
          {
             new float[] {.3f, .3f, .3f, 0, 0},
             new float[] {.59f, .59f, .59f, 0, 0},
             new float[] {.11f, .11f, .11f, 0, 0},
             new float[] {0, 0, 0, 1, 0},
             new float[] {0, 0, 0, 0, 1}
          });

        /// <summary>
        /// 获取灰度图片
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        private Bitmap GetGrayScaleVersion(Image original)
        {
            //http://www.switchonthecode.com/tutorials/csharp-tutorial-convert-a-color-image-to-grayscale
            //create a blank bitmap the same size as original
            Bitmap newBitmap = new Bitmap(original.Width, original.Height);

            //get a graphics object from the new image
            using (Graphics g = Graphics.FromImage(newBitmap))
            {
                //create some image attributes
                ImageAttributes attributes = new ImageAttributes();

                //set the color matrix attribute
                attributes.SetColorMatrix(ColorMatrix);

                //draw the original image on the new image
                //using the grayscale color matrix
                g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
                   0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
            }
            return newBitmap;
        }
    }
}
