﻿

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
            TCPPort = cfg.GetValue<int>("TCPPort" , 9999);
            UpdatesPerSecond = cfg.GetValue<int>("UpdatesPerSecond" , 0);
            PitchEnable = cfg.GetValue<int>("PitchEnable" , 2);
            RollEnable = cfg.GetValue<int>("RollEnable" , 2);
            YawEnable = cfg.GetValue<int>("YawEnable" , 2);
            TXEnable = cfg.GetValue<int>("TXEnable", 2);
            TYEnable = cfg.GetValue<int>("TYEnable", 2);
            TZEnable = cfg.GetValue<int>("TZEnable", 2);
            WheelSteerEnable = cfg.GetValue<int>("WheelSteerEnable" , 2);
            ThrottleEnable = cfg.GetValue<int>("ThrottleEnable" , 2);
            WheelThrottleEnable = cfg.GetValue<int>("WheelThrottleEnable" , 2);
            SASTol = cfg.GetValue<double>("SASTol", 0.05);
        }

        public void OnDisable()
        {
            cfg.save();
        }
    }
}
