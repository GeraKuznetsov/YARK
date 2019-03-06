

using KSP.IO;
using UnityEngine;

namespace KSP_YARK
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    class Config : MonoBehaviour
    {
        public static PluginConfiguration cfg;
        public static int TCPPort;
        public static int UpdatesPerSecond;
        public static int OrbitPlanSkipRate;

        void Awake()
        {
            cfg = PluginConfiguration.CreateForType<Config>();
            cfg.load();
            TCPPort = cfg.GetValue<int>("TCPPort", 9999);
            UpdatesPerSecond = cfg.GetValue<int>("UpdatesPerSecond", 0);
            OrbitPlanSkipRate = cfg.GetValue<int>("UpdatesPerSecond", 1);
        }

        public void OnDisable()
        {
            cfg.save();
        }
    }
}
