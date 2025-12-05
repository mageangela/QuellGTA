using Stylet;
using System.Windows;

namespace GTAFingerprinterCore.Extensions
{
    public static class IWindowManagerExtensions
    {
        public static void ShowErrorMessageBox(this IWindowManager windowManager, string message)
        {
            windowManager.ShowMessageBox(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}