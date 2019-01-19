using System;
using UnityEngine;
using System.Collections.Generic;
using static KSP_PLUGIN.Structs;


namespace KSP_PLUGIN
{
    class Util
    {
        public static AxisControls CPToAC(ControlPacket cp)
        {
            AxisControls ac = new AxisControls
            {
                SASTol = cp.SASTol,

                RotMode = cp.ControlerMode & 0b00000011,
                Pitch = (float)cp.Pitch / 1000.0F,
                Roll = (float)cp.Roll / 1000.0F,
                Yaw = (float)cp.Yaw / 1000.0F,

                TransMode = cp.ControlerMode & 0b00001100 >> 2,
                TX = (float)cp.TX / 1000.0F,
                TY = (float)cp.TY / 1000.0F,
                TZ = (float)cp.TZ / 1000.0F,

                ThrottleMode = cp.ControlerMode & 0b00110000 >> 4,
                Throttle = (float)cp.Throttle / 1000.0F,

                WheelMode = cp.ControlerMode & 0b11000000 >> 6,
                WheelSteer = (float)cp.WheelSteer / 1000.0F,
                WheelThrottle = (float)cp.WheelThrottle / 1000.0F,
            };
            return ac;
        }

        public static VesselControls CPToVC(ControlPacket cp)
        {
            VesselControls vc = new VesselControls
            {
                SAS = (cp.MainControls & (1 << 0)) != 0,
                RCS = (cp.MainControls & (1 << 1)) != 0,
                Lights = (cp.MainControls & (1 << 2)) != 0,
                Gear = (cp.MainControls & (1 << 3)) != 0,
                Brakes = (cp.MainControls & (1 << 4)) != 0,
                Abort = (cp.MainControls & (1 << 5)) != 0,
                Stage = (cp.MainControls & (1 << 6)) != 0,

                SASMode = (int)cp.SASMode,
                SpeedMode = (int)cp.SpeedMode,

                targetHeading = cp.targetHeading,
                targetPitch = cp.targetPitch,
                targetRoll = cp.targetRoll,

                timeWarpRateIndex = cp.timeWarpRateIndex,

                ActionGroups = new Boolean[10]
            };

            for (int j = 0; j < 10; j++)
            {
                vc.ActionGroups[j] = (cp.ActionGroups & (1 << j)) == 1;
            }
            return vc;
        }


        public static byte GetTimeWarpIndex()
        {
            byte timeWarpRateIndex = (byte)TimeWarp.CurrentRateIndex;
            if (timeWarpRateIndex != 0 && TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
            {
                timeWarpRateIndex += 3;
            }
            return timeWarpRateIndex;
        }

        public static void msg(string msg)
        {
            ScreenMessages.PostScreenMessage(msg);
            Debug.Log(msg);
        }

        public static NavHeading WorldVecToNavHeading(Vector3d up, Vector3d north, Vector3d east, Vector3d v)
        {
            NavHeading ret = new NavHeading();
            ret.Pitch = (float)-(Vector3d.Angle(up, v) - 90.0f);
            Vector3d progradeFlat = Vector3d.Exclude(up, v);
            float NAngle = (float)Vector3d.Angle(north, progradeFlat);
            float EAngle = (float)Vector3d.Angle(east, progradeFlat);
            if (EAngle < 90)
                ret.Heading = NAngle;
            else
                ret.Heading = -NAngle + 360;
            return ret;
        }

        public static byte GetSOINumber(string name)
        {
            byte SOI;

            switch (name.ToLower())
            {
                case "sun":
                    SOI = 0;
                    break;
                case "moho":
                    SOI = 1;
                    break;
                case "eve":
                    SOI = 2;
                    break;
                case "gilly":
                    SOI = 3;
                    break;
                case "kerbin":
                    SOI = 4;
                    break;
                case "mun":
                    SOI = 5;
                    break;
                case "minmus":
                    SOI = 6;
                    break;
                case "duna":
                    SOI = 7;
                    break;
                case "ike":
                    SOI = 8;
                    break;
                case "dres":
                    SOI = 9;
                    break;
                case "jool":
                    SOI = 10;
                    break;
                case "laythe":
                    SOI = 11;
                    break;
                case "vall":
                    SOI = 12;
                    break;
                case "tylo":
                    SOI = 13;
                    break;
                case "bop":
                    SOI = 14;
                    break;
                case "pol":
                    SOI = 15;
                    break;
                case "eeloo":
                    SOI = 16;
                    break;
                default:
                    SOI = 0;
                    break;
            }
            return SOI;
        }

        public static byte GetMaxOverHeat(Vessel V)
        {
            byte percent = 0;
            double sPercent = 0, iPercent = 0;
            double percentD = 0, percentP = 0;

            foreach (Part p in V.parts)
            {
                //internal temperature
                iPercent = p.temperature / p.maxTemp;
                //skin temperature
                sPercent = p.skinTemperature / p.skinMaxTemp;

                if (iPercent > sPercent)
                    percentP = iPercent;
                else
                    percentP = sPercent;

                if (percentD < percentP)
                    percentD = percentP;
            }

            percent = (byte)Math.Round(percentD * 100);
            return percent;
        }

        public struct IOResource
        {
            public float Max;
            public float Current;
        }

        public static List<Part> GetListOfActivatedEngines(Vessel vessel)
        {
            var retList = new List<Part>();

            foreach (var part in vessel.Parts)
            {
                foreach (PartModule module in part.Modules)
                {
                    var engineModule = module as ModuleEngines;
                    if (engineModule != null)
                    {
                        if (engineModule.getIgnitionState)
                        {
                            retList.Add(part);
                        }
                    }

                    var engineModuleFx = module as ModuleEnginesFX;
                    if (engineModuleFx != null)
                    {
                        var engineMod = engineModuleFx;
                        if (engineModuleFx.getIgnitionState)
                        {
                            retList.Add(part);
                        }
                    }
                }
            }

            return retList;
        } // this recursive stage look up stuff stolen and modified from KOS and others

        public static double ProspectForResource(String resourceName, List<Part> engines)
        {
            List<Part> visited = new List<Part>();
            double total = 0;

            foreach (var part in engines)
            {
                total += ProspectForResource(resourceName, part, ref visited);
            }

            return total;
        }

        public static double ProspectForResource(String resourceName, Part engine)
        {
            List<Part> visited = new List<Part>();

            return ProspectForResource(resourceName, engine, ref visited);
        }

        public static double ProspectForResource(String resourceName, Part part, ref List<Part> visited)
        {
            double ret = 0;

            if (visited.Contains(part))
            {
                return 0;
            }

            visited.Add(part);

            foreach (PartResource resource in part.Resources)
            {
                if (resource.resourceName.ToLower() == resourceName.ToLower())
                {
                    ret += resource.amount;
                }
            }

            foreach (AttachNode attachNode in part.attachNodes)
            {
                if (attachNode.attachedPart != null //if there is a part attached here
                        && attachNode.nodeType == AttachNode.NodeType.Stack //and the attached part is stacked (rather than surface mounted)
                        && (attachNode.attachedPart.fuelCrossFeed //and the attached part allows fuel flow
                            )
                        && !(part.NoCrossFeedNodeKey.Length > 0 //and this part does not forbid fuel flow
                                && attachNode.id.Contains(part.NoCrossFeedNodeKey))) // through this particular node
                {


                    ret += ProspectForResource(resourceName, attachNode.attachedPart, ref visited);
                }
            }

            return ret;
        }

        public static double ProspectForResourceMax(String resourceName, List<Part> engines)
        {
            List<Part> visited = new List<Part>();
            double total = 0;

            foreach (var part in engines)
            {
                total += ProspectForResourceMax(resourceName, part, ref visited);
            }

            return total;
        }

        public static double ProspectForResourceMax(String resourceName, Part engine)
        {
            List<Part> visited = new List<Part>();

            return ProspectForResourceMax(resourceName, engine, ref visited);
        }

        public static double ProspectForResourceMax(String resourceName, Part part, ref List<Part> visited)
        {
            double ret = 0;

            if (visited.Contains(part))
            {
                return 0;
            }

            visited.Add(part);

            foreach (PartResource resource in part.Resources)
            {
                if (resource.resourceName.ToLower() == resourceName.ToLower())
                {
                    ret += resource.maxAmount;
                }
            }

            foreach (AttachNode attachNode in part.attachNodes)
            {
                if (attachNode.attachedPart != null //if there is a part attached here
                        && attachNode.nodeType == AttachNode.NodeType.Stack //and the attached part is stacked (rather than surface mounted)
                        && (attachNode.attachedPart.fuelCrossFeed //and the attached part allows fuel flow
                            )
                        && !(part.NoCrossFeedNodeKey.Length > 0 //and this part does not forbid fuel flow
                                && attachNode.id.Contains(part.NoCrossFeedNodeKey))) // through this particular node
                {


                    ret += ProspectForResourceMax(resourceName, attachNode.attachedPart, ref visited);
                }
            }

            return ret;
        }

        public static IOResource GetResourceTotal(Vessel V, string resourceName)
        {
            IOResource R = new IOResource();

            foreach (Part p in V.parts)
            {
                foreach (PartResource pr in p.Resources)
                {
                    if (pr.resourceName.Equals(resourceName))
                    {
                        R.Current += (float)pr.amount;
                        R.Max += (float)pr.maxAmount;
                        break;
                    }
                }
            }
            if (R.Max == 0)
                R.Current = 0;

            return R;
        }
    }
}