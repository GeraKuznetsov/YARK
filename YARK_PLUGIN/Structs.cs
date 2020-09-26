using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;


namespace KSP_PLUGIN
{
    public class Structs
    {
        public static readonly byte[] Header_Array = new[] { (byte)0xFF, (byte)0xC4, (byte)'Y', (byte)'A', (byte)'R', (byte)'K', (byte)0x00, (byte)0xFF };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct HeaderArray
        {
            public fixed byte header[8];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)] //used by reciever
        public struct Header
        {
            public HeaderArray header;
            public UInt16 checksum;
            public UInt16 length;
            public byte type;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct StatusPacket
        {
            public int ID;

            public byte status; //Are we loaded in?
            public byte YarkVersion;
            public fixed byte vessalName[32]; //32 bytes

        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct NavHeading
        {
            public float Pitch, Heading;
            public NavHeading(float Pitch, float Heading)
            {
                this.Pitch = Pitch;
                this.Heading = Heading;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct VesselPacket
        {
            public int ID;

            public float deltaTime;

            //##### CRAFT ######
            public byte FlightStatus;

            public float Pitch;
            public float Heading;
            public float Roll;

            //#### NAVBALL VECTOR #######
            public NavHeading Prograde;
            public NavHeading Target;
            public NavHeading Maneuver;

            public byte MainControls;                   //SAS RCS Lights Gear Brakes Abort Stage
            public UInt16 ActionGroups;                   //action groups 1-10 in 2 bytes
            public float VVI;
            public float G;
            public float RAlt;
            public float Alt;
            public float Vsurf;
            public byte MaxOverHeat;    //  Max part overheat (% percent)
            public float IAS;           //  Indicated Air Speed
            public float VOrbit;

            //###### ORBITAL ######
            public OrbitData CurrentOrbit;
            public float Lat;
            public float Lon;

            //###### FUEL #######
            public byte CurrentStage;   //  Current stage number
            public byte TotalStage;     //  TotalNumber of stages
            public float LiquidFuelTot;
            public float LiquidFuel;
            public float OxidizerTot;
            public float Oxidizer;
            public float EChargeTot;
            public float ECharge;
            public float MonoPropTot;
            public float MonoProp;
            public float IntakeAirTot;
            public float IntakeAir;
            public float SolidFuelTot;
            public float SolidFuel;
            public float XenonGasTot;
            public float XenonGas;
            public float LiquidFuelTotS;
            public float LiquidFuelS;
            public float OxidizerTotS;
            public float OxidizerS;

            //### MISC ###
            public float MissionTime;
            public float UT;
            public UInt32 MNTime;
            public float MNDeltaV;
            public byte HasTarget;
            public float TargetDist;    //  Distance to targeted vessel (m)
            public float TargetV;       //  Target vessel relative velocity (m/s)
            public NavHeading TargetRotation;

            public byte SASMode; //hold, prograde, retro, etc...
            public byte SpeedMode; //Surface, orbit target

            public byte timeWarpRateIndex;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ControlPacket
        {
            public int ID;
            public byte MainControls;                   //SAS RCS Lights Gear Brakes Abort Stage
            public UInt16 ActionGroups;                //action groups 1-10 in 2 bytes

            // Throttle and axis controls have the following settings: 
            // 0: The internal value (supplied by KSP) is always used.
            // 1: The external value (read from serial packet) is always used.
            // 2: If the internal value is not zero use it, otherwise use the external value.
            // 3: If the external value is not zero use it, otherwise use the internal value.  
            public byte ControlerMode;                //DDCCBBAA (2 bits each)
            public float SASTol;
            public short Pitch;                        //-1000 -> 1000 //A
            public short Roll;                         //-1000 -> 1000
            public short Yaw;                          //-1000 -> 1000
            public short TX;                           //-1000 -> 1000 //B
            public short TY;                           //-1000 -> 1000
            public short TZ;                           //-1000 -> 1000 
            public short Throttle;                     // 0 -> 1000    //C
            public short WheelSteer;                   //-1000 -> 1000 //D
            public short WheelThrottle;                // 0 -> 1000
            public float targetHeading, targetPitch, targetRoll;        //E
            public byte SASMode; //hold, prograde, retro, etc...
            public byte SpeedMode; //Surface, orbit target
            public byte timeWarpRateIndex;
        };

        public struct VesselControls
        {
            public Boolean SAS;
            public Boolean RCS;
            public Boolean Lights;
            public Boolean Gear;
            public Boolean Brakes;
            public Boolean Abort;
            public Boolean Stage;
            public int Mode;
            public int SASMode;
            public int SpeedMode;
            public Boolean[] ActionGroups;
            public float targetHeading, targetPitch, targetRoll;
            //public Boolean holdTargetVector;
            public byte timeWarpRateIndex;
            public bool useTargetValues;

            public VesselControls(bool ignore)
            {
                /*holdTargetVector =*/
                SAS = RCS = Lights = Gear = Brakes /*= Precision*/ = Abort = Stage = useTargetValues = false;
                Mode = SASMode = SpeedMode = 0;
                timeWarpRateIndex = 1;
                ActionGroups = new Boolean[10];
                targetHeading = targetPitch = targetRoll = 0;
                //targetHeading = targetPitch = targetRoll = Pitch = Roll = Yaw = TX = TY = TZ = WheelSteer = Throttle = WheelThrottle = 0;
            }
        };

        public struct AxisControls
        {
            public float SASTol;

            public int RotMode;
            public float Pitch; //1
            public float Roll;
            public float Yaw;

            public int TransMode;
            public float TX; //2
            public float TY;
            public float TZ;

            public int ThrottleMode;
            public float Throttle;

            public int WheelMode;
            public float WheelThrottle;
            public float WheelSteer;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct OrbitData
        {
            public byte SOINumber;
            public double longOfAscNode;
            public double argOfPE;
            public double SemiLatusRectum;
            public double e;
            public float inc;
            public double anomoly;
            public double anomolyEnd;
            public float AP;
            public float PE;
            public int T2Pe;
            public int T2AN;
            public int T2DN;
            public int period;
            public int T2PatchEnd;
            public byte transStart;
            public byte transEnd;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ManData
        {
            public float DV;
            public double UT;
            public float X, Y, Z;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ClosestAprouchData
        {
            public float ANAnom; //anomoly of ascending node between orbits
            public float TargetANAnom;
            public int T2AN;
            public int T2DN;
            public float RelInc; //relative inclonation
            public float CAAnom; //anomoly of closest aprouch
            public float TargetCAAnom;
            public int T2CA; //closest aprouch
            public int SepAtCA;
        }

        public struct RawOrbitPlanData
        {
            public List<OrbitData> CurrentOrbitPatches;
            public int ManPatchNum;
            public List<OrbitData> PlannedOrbitPatches;
            public OrbitData TargetOrbit;
            public string TargetName;
            public List<ManData> Mans;
            public ClosestAprouchData Rendezvous;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ManChangePacket
        {
            public byte mode; //0=set //1=new //2=delete
            public byte manID;
            public double UT;
            public float X, Y, Z;
        };
    }
}
