using GTAFingerprinterCore.Implementions;
using GTAFingerprinterCore.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GTAFingerprinterCore.Interfaces
{
    public interface IKeyboard
    {
        Task Press(Keys key, int count = 1, int delay = 20);

        IDictionary<string, HotKey> HotKeys { get; }
    }
}