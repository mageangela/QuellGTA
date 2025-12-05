using GTAFingerprinterCore.Configurations;
using GTAFingerprinterCore.Extensions;
using GTAFingerprinterCore.Implementions;
using GTAFingerprinterCore.Interfaces;
using GTAFingerprinterCore.Pages;
using Stylet;
using StyletIoC;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GTAFingerprinterCore
{
    public class Bootstrapper : Bootstrapper<RootViewModel>
    {
        public static readonly string configFilePath = Path.Combine(Environment.CurrentDirectory, "appsettings.json");

        protected override void ConfigureIoC(IStyletIoCBuilder builder)
        {
            base.ConfigureIoC(builder);

            #region 加载模板文件

            var resources = new ResourceDictionary
            {
                Source = new Uri("Resources/AppRes.xaml", UriKind.Relative)
            };
            var diamonds = new Dictionary<Bitmap, IList<Bitmap>>();
            for (int i = 1; i <= 4; i++)
            {
                var main = (resources[$"diamond{i}"] as BitmapImage).ToBitmap();
                var subs = new Collection<Bitmap>();
                for (int j = 1; j <= 4; j++)
                {
                    var sub = (resources[$"diamond{i}{j}"] as BitmapImage).ToBitmap();
                    subs.Add(sub);
                }
                diamonds[main] = subs;
            }

            var pericos = new Dictionary<Bitmap, IList<Bitmap>>();
            for (int i = 1; i <= 7; i++)
            {
                var main = (resources[$"perico{i}"] as BitmapImage).ToBitmap();
                var subs = new Collection<Bitmap>();
                for (int j = 1; j <= 8; j++)
                {
                    var sub = (resources[$"perico{i}{j}"] as BitmapImage).ToBitmap();
                    subs.Add(sub);
                }
                pericos[main] = subs;
            }


            #endregion 加载模板文件

            try
            {
                var config = AppConfig.FromJsonFile(configFilePath);
                builder.Bind<AppConfig>().ToInstance(config);
            }
            catch (Exception)
            {
                // MessageBox.Show($"读取配置文件失败\n{e.Message}\n使用默认推荐配置", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                builder.Bind<AppConfig>().ToInstance(new AppConfig());
            }
            builder.Bind<DiamondTemplatesMap>().ToInstance(new DiamondTemplatesMap(diamonds));
            builder.Bind<PericoTemplatesMap>().ToInstance(new PericoTemplatesMap(pericos));
            builder.Bind<IKeyboard>().ToInstance(new WinApiKeyboard());
            builder.Bind<IMouse>().ToInstance(new WinApiMouse());
            builder.Bind<IScreenCapture>().ToInstance(new WinApiScreenCapture());
            builder.Bind<IImageComparer>().To<DHashImageComparer>();
            builder.Bind<IDiamondFingerprinter>().To<DiamondFingerprinter>();
            builder.Bind<IPericoFingerprinter>().To<PericoFingerprinter>();
        }
    }
}