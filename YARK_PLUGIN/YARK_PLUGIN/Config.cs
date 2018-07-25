

using KSP.IO;
using UnityEngine;

namespace KSP_YARK
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    class Config:MonoBehaviour
    {
        public static PluginConfiguration cfg;
        // Throttle and axis controls have the following settings:
        // 0: The internal value (supplied by KSP) is always used.
        // 1: The external value (read from serial packet) is always used.
        // 2: If the internal value is not zero use it, otherwise use the external value.
        // 3: If the external value is not zero use it, otherwise use the internal value.        
        public static int TCPPort;
        public static int UpdatesPerSecond;
        public static int PitchEnable;
        public static int RollEnable;
        public static int YawEnable;
        public static int TXEnable;
        public static int TYEnable;
        public static int TZEnable;
        public static int WheelSteerEnable;
        public static int ThrottleEnable;
        public static int WheelThrottleEnable;
        public static double SASTol;

        void Awake()
        {
            cfg = PluginConfiguration.CreateForType<Config>();
            cfg.load();
            TCPPort = cfg.GetValue<int>("TCPPort");
            UpdatesPerSecond = cfg.GetValue<int>("UpdatesPerSecond");
            PitchEnable = cfg.GetValue<int>("PitchEnable");
            RollEnable = cfg.GetValue<int>("RollEnable");
            YawEnable = cfg.GetValue<int>("YawEnable");
            TXEnable = cfg.GetValue<int>("TXEnable");
            TYEnable = cfg.GetValue<int>("TYEnable");
            TZEnable = cfg.GetValue<int>("TZEnable");
            WheelSteerEnable = cfg.GetValue<int>("WheelSteerEnable");
            ThrottleEnable = cfg.GetValue<int>("ThrottleEnable");
            WheelThrottleEnable = cfg.GetValue<int>("WheelThrottleEnable");
            SASTol = cfg.GetValue<double>("SASTol");
        }

    }
}
