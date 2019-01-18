using KSP_YARK;

namespace KSP_PLUGIN
{
    class AxisInput
    {
        public static bool holdTargetVector = false;
        public static float Throttle = 0, Pitch = 0, Yaw = 0, Roll = 0, TX = 0, TY = 0, TZ = 0, WheelSteer = 0, WheelThrottle = 0;

        public static float targetHeading, targetRoll, targetPitch;

        public static void Callback(FlightCtrlState s)
        {
            switch (Config.ThrottleEnable)
            {
                case 1:
                    s.mainThrottle = Throttle;
                    break;
                case 2:
                    if (s.mainThrottle == 0)
                    {
                        s.mainThrottle = Throttle;
                    }
                    break;
                case 3:
                    if (Throttle != 0)
                    {
                        s.mainThrottle = Throttle;
                    }
                    break;
                default:
                    break;
            }

            if (!holdTargetVector)
            {
                switch (Config.PitchEnable)
                {
                    case 1:
                        s.pitch = Pitch;
                        break;
                    case 2:
                        if (s.pitch == 0)
                            s.pitch = Pitch;
                        break;
                    case 3:
                        if (Pitch != 0)
                            s.pitch = Pitch;
                        break;
                    default:
                        break;
                }

                switch (Config.RollEnable)
                {
                    case 1:
                        s.roll = Roll;
                        break;
                    case 2:
                        if (s.roll == 0)
                            s.roll = Roll;
                        break;
                    case 3:
                        if (Roll != 0)
                            s.roll = Roll;
                        break;
                    default:
                        break;
                }

                switch (Config.YawEnable)
                {
                    case 1:
                        s.yaw = Yaw;
                        break;
                    case 2:
                        if (s.yaw == 0)
                            s.yaw = Yaw;
                        break;
                    case 3:
                        if (Yaw != 0)
                            s.yaw = Yaw;
                        break;
                    default:
                        break;
                }
            }

            switch (Config.TXEnable)
            {
                case 1:
                    s.X = TX;
                    break;
                case 2:
                    if (s.X == 0)
                        s.X = TX;
                    break;
                case 3:
                    if (TX != 0)
                        s.X = TX;
                    break;
                default:
                    break;
            }

            switch (Config.TYEnable)
            {
                case 1:
                    s.Y = TY;
                    break;
                case 2:
                    if (s.Y == 0)
                        s.Y = TY;
                    break;
                case 3:
                    if (TY != 0)
                        s.Y = TY;
                    break;
                default:
                    break;
            }

            switch (Config.TZEnable)
            {
                case 1:
                    s.Z = TZ;
                    break;
                case 2:
                    if (s.Z == 0)
                        s.Z = TZ;
                    break;
                case 3:
                    if (TZ != 0)
                        s.Z = TZ;
                    break;
                default:
                    break;
            }

            switch (Config.WheelSteerEnable)
            {
                case 1:
                    s.wheelSteer = WheelSteer;
                    break;
                case 2:
                    if (s.wheelSteer == 0)
                    {
                        s.wheelSteer = WheelSteer;
                    }
                    break;
                case 3:
                    if (WheelSteer != 0)
                    {
                        s.wheelSteer = WheelSteer;
                    }
                    break;
                default:
                    break;
            }

            switch (Config.WheelThrottleEnable)
            {
                case 1:
                    s.wheelThrottle = WheelThrottle;
                    break;
                case 2:
                    if (s.wheelThrottle == 0)
                    {
                        s.wheelThrottle = WheelThrottle;
                    }
                    break;
                case 3:
                    if (WheelThrottle != 0)
                    {
                        s.wheelThrottle = WheelThrottle;
                    }
                    break;
                default:
                    break;
            }
        }
    }
}
