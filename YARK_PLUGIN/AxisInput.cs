using KSP_YARK;
using System;
using UnityEngine;
using static KSP_PLUGIN.Structs;

namespace KSP_PLUGIN
{
    class AxisInput
    {
        public static bool holdTargetVector = false; //static used by main
        public static float targetHeading, targetRoll, targetPitch;
        public static bool SupressSAS;
        public static void Callback(FlightCtrlState s)
        {
            SupressSAS = false;
            if (Main.conn == null) return;
            AxisControls ac = Main.conn.GetAxisControls();

            switch (ac.ThrottleMode)
            {
                case 1:
                    s.mainThrottle = ac.Throttle;
                    break;
                case 2:
                    if (s.mainThrottle == 0) s.mainThrottle = ac.Throttle;
                    break;
                case 3:
                    if (ac.Throttle != 0) s.mainThrottle = ac.Throttle;
                    break;
                default:
                    break;
            }

            if (!holdTargetVector)
            {
                switch (ac.RotMode)
                {
                    case 1:
                        s.pitch = ac.Pitch;
                        s.roll = ac.Roll;
                        s.yaw = ac.Yaw;
                        break;
                    case 2:
                        if (s.pitch == 0) s.pitch = ac.Pitch;
                        if (s.roll == 0) s.roll = ac.Roll;
                        if (s.yaw == 0) s.yaw = ac.Yaw;
                        break;
                    case 3:
                        if (ac.Pitch != 0) s.pitch = ac.Pitch;
                        if (ac.Roll != 0) s.roll = ac.Roll;
                        if (ac.Yaw != 0) s.yaw = ac.Yaw;
                        break;
                    default:
                        break;
                }
                if (ac.RotMode != 0)
                {
                    SupressSAS = (Math.Abs(ac.Pitch) > ac.SASTol || Math.Abs(ac.Roll) > ac.SASTol || Math.Abs(ac.Yaw) > ac.SASTol);
                }
            }

            switch (ac.TransMode)
            {
                case 1:
                    s.X = ac.TX;
                    s.Y = ac.TY;
                    s.Z = ac.TZ;
                    break;
                case 2:
                    if (s.X == 0) s.X = ac.TX;
                    if (s.Y == 0) s.Y = ac.TY;
                    if (s.Z == 0) s.Z = ac.TZ;
                    break;
                case 3:
                    if (ac.TX != 0) s.X = ac.TX;
                    if (ac.TY != 0) s.Y = ac.TY;
                    if (ac.TZ != 0) s.Z = ac.TZ;
                    break;
                default:
                    break;
            }

            switch (ac.WheelMode)
            {
                case 1:
                    s.wheelSteer = ac.WheelSteer;
                    s.wheelThrottle = ac.WheelThrottle;
                    break;
                case 2:
                    if (s.wheelSteer == 0) s.wheelSteer = ac.WheelSteer;
                    if (s.wheelThrottle == 0) s.wheelThrottle = ac.WheelThrottle;
                    break;
                case 3:
                    if (ac.WheelSteer != 0) s.wheelSteer = ac.WheelSteer;
                    if (ac.WheelThrottle != 0) s.wheelThrottle = ac.WheelThrottle;
                    break;
                default:
                    break;
            }

        }
    }
}
