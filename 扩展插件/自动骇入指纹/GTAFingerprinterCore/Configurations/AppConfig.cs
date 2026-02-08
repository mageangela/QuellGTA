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

        // 添加默认值初始化
        public DiamondConfig Diamond { get; set; } = new DiamondConfig();
        public PericoConfig Perico { get; set; } = new PericoConfig();

        public static AppConfig FromJsonFile(string filename)
        {
            try
            {
                if (!File.Exists(filename))
                {
                    // 如果文件不存在，创建默认配置并保存
                    var defaultConfig = new AppConfig();
                    defaultConfig.SaveConfig(filename);
                    return defaultConfig;
                }

                var json = File.ReadAllText(filename);
                var config = JsonConvert.DeserializeObject<AppConfig>(json);

                // 确保配置对象不为null
                config ??= new AppConfig();

                // 确保子配置不为null
                config.Diamond ??= new DiamondConfig();
                config.Perico ??= new PericoConfig();

                return config;
            }
            catch (Exception ex)
            {
                // 记录错误并返回默认配置
                // 可以在这里添加日志记录
                return new AppConfig();
            }
        }

        public void SaveConfig(string filename)
        {
            try
            {
                // 确保配置对象完整
                Diamond ??= new DiamondConfig();
                Perico ??= new PericoConfig();

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
