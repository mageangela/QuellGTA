using GTAFingerprinterCore.Implementions;
using GTAFingerprinterCore.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace GTAFingerprinterCore.Configurations
{
    public class AppConfig
    {
        public bool IsFullScreen { get; set; } = false;
        public Keys RecognizeKey { get; set; } = Keys.F10;
        public int KeyPressDelay { get; set; } = 40;
        public float Similarity { get; set; } = 0.825f;
        public DiamondConfig Diamond { get; set; }
        public PericoConfig Perico { get; set; }

        public static AppConfig FromJsonFile(string filename)
        {
            try
            {
                var json = File.ReadAllText(filename);
                var config = JsonConvert.DeserializeObject<AppConfig>(json);
                return config;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void SaveConfig(string filename)
        {
            try
            {
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(filename, json);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
    public class DiamondConfig
    {
        public ImgConfig MainImg { get; set; } = new ImgConfig
        {
            X = 624,
            Y = 104,
            Width = 271,
            Height = 350
        };
        public ImgConfig SubImg { get; set; } = new ImgConfig
        {
            X = 315,
            Y = 179,
            Width = 81,
            Space = 15
        };
    }
    public class PericoConfig
    {
        public ImgConfig MainImg { get; set; } = new ImgConfig
        {
            X = 624,
            Y = 104,
            Width = 271,
            Height = 350
        };
        public ImgConfig SubImg { get; set; } = new ImgConfig
        {
            X = 550,
            Y = 476,
            Width = 545,
            Height = 81,
            Space = 20
        };
    }

    public class ImgConfig
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Space { get; set; }
    }
}
