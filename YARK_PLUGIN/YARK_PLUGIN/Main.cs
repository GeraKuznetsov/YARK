using System;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using KSP.UI.Screens;

namespace KSP_YARK
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class YARK : MonoBehaviour
    {
        TcpListener server;
        TcpClient client;
        NetworkStream ns;
        bool conn;
        KSPData KD;
        StatusChange SC;
        VesselControls VC, VCOld;
        bool inFlight, virginConection;
        public static Vessel AV;
        Vector3d CoM, north, up, east;
        IOResource TempR;
        Boolean wasSASOn = false, forceSASMode = false, newDataRec = false;
        int lastSASMode = 1;
        float TimeOFLastSend;
        UInt32 currentTime, lastTime;

        public void Awake()
        {
            TempR = new IOResource();
            DontDestroyOnLoad(gameObject);
            msg("Starting YARK KSPWebsockIO");

            Header h = new Header();

            unsafe
            {
                for (int i = 0; i < Header_Array.Length; i++)
                {
                    h.header[i] = Header_Array[i];
                }
            }


            SC = new StatusChange
            {
                header = h,
                packetType = 0x01,
                ID = 0
            };

            KD = new KSPData
            {
                header = h,
                packetType = 0x02,
                ID = 0
            };

            server = new TcpListener(IPAddress.Any, Config.TCPPort);
            server.Start();
            conn = false;
        }

        public void Update()
        {
            if (forceSASMode)
            {
                forceSASMode = false;
                SetSASMode(lastSASMode);
            }
            if (conn)
            {
                if (!client.Connected) //TODO: IMPLEMENT TIMEOUT       
                {
                    if (AV != null)
                    {
                        AV.OnPostAutopilotUpdate -= AxisInput;
                    }
                    msg("YARK: Client disconnected");
                    conn = false;
                }
                else
                {
                    if (SceneManager.GetActiveScene().buildIndex == 7) //in flight?
                    {
                        currentTime = (UInt32)Planetarium.GetUniversalTime();
                        if (virginConection || !inFlight || AV.id != FlightGlobals.ActiveVessel.id || lastTime > currentTime)
                        {
                            virginConection = false;
                            if (AV != null)
                            {
                                AV.OnPostAutopilotUpdate -= AxisInput;
                            }
                            AV = FlightGlobals.ActiveVessel;
                            AV.OnPostAutopilotUpdate += AxisInput;

                            //sync inputs on vessel switch
                            ControlPacket cp = new ControlPacket
                            {
                                MainControls = CalcMainControls(),
                                ActionGroups = CalcActionGroups(),
                                SASMode = GetSASMode(),
                                SpeedMode = (byte)(FlightGlobals.speedDisplayMode + 1),
                                timeWarpRateIndex = GetTimeWarpIndex()
                            };
                            cp.targetHeading = cp.targetPitch = cp.targetRoll = cp.WheelSteer = cp.WheelThrottle = cp.Throttle = cp.Pitch = cp.Roll = cp.Yaw = cp.TX = cp.TY = cp.TZ;
                            VC = VCOld = CPToVC(cp);

                            msg("YARK: vessal resync");
                            inFlight = true;
                            SendNewStatus();
                        }
                        lastTime = currentTime;
                        SendKD();
                        if (newDataRec)
                        {
                            UpdateControls();
                            newDataRec = false;
                        }
                    }
                    else
                    {
                        if (virginConection || inFlight)
                        {
                            virginConection = false;
                            inFlight = false;
                            SendNewStatus();
                        }
                    }
                    ServerReceive();
                }
            }
            else if (server.Pending())
            {
                msg("YARK: Client connected");

                //VC = new VesselControls(false);
                //VCOld = new VesselControls(false);

                client = server.AcceptTcpClient();
                ns = client.GetStream();
                conn = true;
                virginConection = true;
                newDataRec = false;
                TimeOFLastSend = Time.unscaledTime;
                lastTime = 0;
            }
        }
        /*  public void OnDisable()
          {
              msg("YARK: stopping");
          }*/

        private void SendNewStatus()
        {
            SC.ID++;
            SC.status = inFlight ? (byte)1 : (byte)0;
            char[] name = inFlight ? AV.vesselName.ToCharArray() : "null".ToCharArray();

            unsafe
            {
                fixed (byte* charPtr = SC.vessalName)
                {
                    int i = 0;
                    for (; i < Math.Min(15, name.Length); i++)
                    {
                        *(charPtr + i) = (byte)name[i];
                    }
                    *(charPtr + i) = (byte)0x00;
                }
            }

            int size = Marshal.SizeOf(SC);
            byte[] arr = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(SC, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            ns.Write(arr, 0, arr.Length);
        }

        private void SendKD()
        {
            float time = Time.unscaledTime;
            KD.deltaTime = time - TimeOFLastSend;
            if (Config.UpdatesPerSecond != 0 && ((KD.deltaTime) < (1.0f / (float)(Config.UpdatesPerSecond))))
            {
                return;
            }
            TimeOFLastSend = time;

            List<Part> ActiveEngines = new List<Part>();
            ActiveEngines = GetListOfActivatedEngines(AV);

            KD.AP = (float)AV.orbit.ApA;
            KD.PE = (float)AV.orbit.PeA;
            KD.SemiMajorAxis = (float)AV.orbit.semiMajorAxis;
            KD.SemiMinorAxis = (float)AV.orbit.semiMinorAxis;
            KD.e = (float)AV.orbit.eccentricity;
            KD.inc = (float)AV.orbit.inclination;
            KD.VVI = (float)AV.verticalSpeed;
            KD.G = (float)AV.geeForce;
            KD.TAp = (int)Math.Round(AV.orbit.timeToAp);
            KD.TPe = (int)Math.Round(AV.orbit.timeToPe);
            KD.TrueAnomaly = (float)AV.orbit.trueAnomaly;
            KD.period = (int)Math.Round(AV.orbit.period);

            double ASL = AV.mainBody.GetAltitude(AV.CoM);
            double AGL = (ASL - AV.terrainAltitude);

            if (AGL < ASL)
                KD.RAlt = (float)AGL;
            else
                KD.RAlt = (float)ASL;

            KD.Alt = (float)ASL;
            KD.Vsurf = (float)AV.srfSpeed;
            KD.Lat = (float)AV.latitude;
            KD.Lon = (float)AV.longitude;

            TempR = GetResourceTotal(AV, "LiquidFuel");
            KD.LiquidFuelTot = TempR.Max;
            KD.LiquidFuel = TempR.Current;

            KD.LiquidFuelTotS = (float)ProspectForResourceMax("LiquidFuel", ActiveEngines);
            KD.LiquidFuelS = (float)ProspectForResource("LiquidFuel", ActiveEngines);

            TempR = GetResourceTotal(AV, "Oxidizer");
            KD.OxidizerTot = TempR.Max;
            KD.Oxidizer = TempR.Current;

            KD.OxidizerTotS = (float)ProspectForResourceMax("Oxidizer", ActiveEngines);
            KD.OxidizerS = (float)ProspectForResource("Oxidizer", ActiveEngines);

            TempR = GetResourceTotal(AV, "ElectricCharge");
            KD.EChargeTot = TempR.Max;
            KD.ECharge = TempR.Current;
            TempR = GetResourceTotal(AV, "MonoPropellant");
            KD.MonoPropTot = TempR.Max;
            KD.MonoProp = TempR.Current;
            TempR = GetResourceTotal(AV, "IntakeAir");
            KD.IntakeAirTot = TempR.Max;
            KD.IntakeAir = TempR.Current;
            TempR = GetResourceTotal(AV, "SolidFuel");
            KD.SolidFuelTot = TempR.Max;
            KD.SolidFuel = TempR.Current;
            TempR = GetResourceTotal(AV, "XenonGas");
            KD.XenonGasTot = TempR.Max;
            KD.XenonGas = TempR.Current;

            KD.MissionTime = (UInt32)Math.Round(AV.missionTime);

            KD.VOrbit = (float)AV.orbit.GetVel().magnitude;

            KD.MNTime = 0;
            KD.MNDeltaV = 0;
            KD.TargetDist = 0;
            KD.TargetV = 0;

            KD.HasTarget = TargetExists() ? (byte)1 : (byte)0;

            //mathy stuff
            Quaternion rotationSurface;
            CoM = AV.CoM;
            up = (CoM - AV.mainBody.position).normalized;
            north = Vector3d.Exclude(up, (AV.mainBody.position + AV.mainBody.transform.up * (float)AV.mainBody.Radius) - CoM).normalized;

            east = Vector3d.Cross(up, north);

            rotationSurface = Quaternion.LookRotation(north, up);

            Vector3d attitude = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(AV.GetTransform().rotation) * rotationSurface).eulerAngles;

            KD.Roll = (float)((attitude.z > 180) ? (attitude.z - 360.0) : attitude.z);
            KD.Pitch = (float)((attitude.x > 180) ? (360.0 - attitude.x) : -attitude.x);
            KD.Heading = (float)attitude.y;

            Vector3d prograde = new Vector3d(0, 0, 0);
            switch (FlightGlobals.speedDisplayMode)
            {
                case FlightGlobals.SpeedDisplayModes.Surface:
                    prograde = AV.srf_velocity.normalized;
                    break;
                case FlightGlobals.SpeedDisplayModes.Orbit:
                    prograde = AV.obt_velocity.normalized;
                    break;
                case FlightGlobals.SpeedDisplayModes.Target:
                    prograde = FlightGlobals.ship_tgtVelocity;
                    break;
            }

            KD.Prograde = WorldVecToNavHeading(up, north, east, prograde);

            if (TargetExists())
            {
                KD.Target = WorldVecToNavHeading(up, north, east, AV.targetObject.GetTransform().position - AV.transform.position);
                KD.TargetDist = (float)Vector3.Distance(FlightGlobals.fetch.VesselTarget.GetVessel().transform.position, AV.transform.position);
                KD.TargetV = (float)FlightGlobals.ship_tgtVelocity.magnitude;

            }
            //KD.NormalHeading = WorldVecToNavHeading(up, north, east, Vector3d.Cross(AV.obt_velocity.normalized, up)).Heading;

            if (AV.patchedConicSolver != null)
            {
                if (AV.patchedConicSolver.maneuverNodes != null)
                {
                    if (AV.patchedConicSolver.maneuverNodes.Count > 0)
                    {
                        KD.MNTime = (UInt32)Math.Round(AV.patchedConicSolver.maneuverNodes[0].UT - Planetarium.GetUniversalTime());
                        KD.MNDeltaV = (float)AV.patchedConicSolver.maneuverNodes[0].GetBurnVector(AV.patchedConicSolver.maneuverNodes[0].patch).magnitude; //Added JS

                        KD.Maneuver = WorldVecToNavHeading(up, north, east, AV.patchedConicSolver.maneuverNodes[0].GetBurnVector(AV.patchedConicSolver.maneuverNodes[0].patch));
                    }
                }
            }

            KD.MainControls = CalcMainControls();

            KD.ActionGroups = CalcActionGroups();

            if (AV.orbit.referenceBody != null)
            {
                KD.SOINumber = GetSOINumber(AV.orbit.referenceBody.name);
            }

            KD.MaxOverHeat = GetMaxOverHeat(AV);
            KD.IAS = (float)AV.indicatedAirSpeed;

            KD.CurrentStage = (byte)StageManager.CurrentStage;
            KD.TotalStage = (byte)StageManager.StageCount;

            KD.SpeedMode = (byte)(FlightGlobals.speedDisplayMode + 1);
            KD.SASMode = GetSASMode();

            KD.timeWarpRateIndex = GetTimeWarpIndex();


            KD.ID++;

            int size = Marshal.SizeOf(KD);
            byte[] arr = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(KD, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            ns.Write(arr, 0, arr.Length);
        }

        byte CalcMainControls()
        {
            byte MainControls = 0;
            MainControls |= (byte)(((AV.ActionGroups[KSPActionGroup.SAS] ? 1 : 0)) << 0);
            MainControls |= (byte)(((AV.ActionGroups[KSPActionGroup.RCS] ? 1 : 0)) << 1);
            MainControls |= (byte)(((AV.ActionGroups[KSPActionGroup.Light] ? 1 : 0)) << 2);
            MainControls |= (byte)(((AV.ActionGroups[KSPActionGroup.Gear] ? 1 : 0)) << 3);
            MainControls |= (byte)(((AV.ActionGroups[KSPActionGroup.Brakes] ? 1 : 0)) << 4);
            MainControls |= (byte)(((AV.ActionGroups[KSPActionGroup.Abort] ? 1 : 0)) << 5);
            MainControls |= (byte)(((AV.ActionGroups[KSPActionGroup.Stage] ? 1 : 0)) << 6);
            return MainControls;
        }

        UInt16 CalcActionGroups()
        {
            UInt16 ActionGroups = 0;
            ActionGroups |= (UInt16)(((AV.ActionGroups[KSPActionGroup.Custom01] ? 1 : 0)) << 0);
            ActionGroups |= (UInt16)(((AV.ActionGroups[KSPActionGroup.Custom02] ? 1 : 0)) << 1);
            ActionGroups |= (UInt16)(((AV.ActionGroups[KSPActionGroup.Custom03] ? 1 : 0)) << 2);
            ActionGroups |= (UInt16)(((AV.ActionGroups[KSPActionGroup.Custom04] ? 1 : 0)) << 3);
            ActionGroups |= (UInt16)(((AV.ActionGroups[KSPActionGroup.Custom05] ? 1 : 0)) << 4);
            ActionGroups |= (UInt16)(((AV.ActionGroups[KSPActionGroup.Custom06] ? 1 : 0)) << 5);
            ActionGroups |= (UInt16)(((AV.ActionGroups[KSPActionGroup.Custom07] ? 1 : 0)) << 6);
            ActionGroups |= (UInt16)(((AV.ActionGroups[KSPActionGroup.Custom08] ? 1 : 0)) << 7);
            ActionGroups |= (UInt16)(((AV.ActionGroups[KSPActionGroup.Custom09] ? 1 : 0)) << 8);
            ActionGroups |= (UInt16)(((AV.ActionGroups[KSPActionGroup.Custom10] ? 1 : 0)) << 9);
            return ActionGroups;
        }

        private void UpdateControls()
        {
            if (VC.RCS != VCOld.RCS)
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.RCS, VC.RCS);
                VCOld.RCS = VC.RCS;
            }
            if (VC.SAS != VCOld.SAS)
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.SAS, VC.SAS);
                VCOld.SAS = VC.SAS;
            }
            if (VC.Lights != VCOld.Lights)
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Light, VC.Lights);
                VCOld.Lights = VC.Lights;
            }
            if (VC.Gear != VCOld.Gear)
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Gear, VC.Gear);
                VCOld.Gear = VC.Gear;
            }
            if (VC.Brakes != VCOld.Brakes)
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Brakes, VC.Brakes);
                VCOld.Brakes = VC.Brakes;
            }
            if (VC.Abort != VCOld.Abort)
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Abort, VC.Abort);
                VCOld.Abort = VC.Abort;
            }
            if (VC.Stage != VCOld.Stage)
            {
                if (VC.Stage)
                    StageManager.ActivateNextStage();

                AV.ActionGroups.SetGroup(KSPActionGroup.Stage, VC.Stage);
                VCOld.Stage = VC.Stage;
            }

            //================ control groups

            for (int j = 0; j < 10; j++)
            {
                if (VC.ActionGroups[j] != VCOld.ActionGroups[j])
                {
                    AV.ActionGroups.SetGroup((KSPActionGroup)(1 << (7 + j)), VC.ActionGroups[1]);
                    VCOld.ActionGroups[j] = VC.ActionGroups[j];
                }
            }

            //Set TimeWarp rate
            if (VC.timeWarpRateIndex != VCOld.timeWarpRateIndex)
            {
                byte mode = VC.timeWarpRateIndex;
                if (mode >= 0 && mode <= 10)
                {
                    if (mode > 3)
                    {
                        TimeWarp.fetch.Mode = TimeWarp.Modes.HIGH;
                        TimeWarp.SetRate(mode - 3, false);
                    }
                    else
                    {
                        TimeWarp.fetch.Mode = TimeWarp.Modes.LOW;
                        TimeWarp.SetRate(mode, false);
                    }
                }
                VCOld.timeWarpRateIndex = VC.timeWarpRateIndex;
            }

            //Set sas mode
            if (VC.SASMode != VCOld.SASMode)
            {
                int setTo = VC.SASMode;
                if (setTo == 11)
                {
                    setTo = 1;
                    VC.holdTargetVector = true;
                }
                else
                {
                    VC.holdTargetVector = false;
                }
                if (setTo != 0 && setTo < 11)
                {
                    SetSASMode(setTo);
                }
                VCOld.SASMode = VC.SASMode;
            }

            //set navball mode
            if (VC.SpeedMode != VCOld.SpeedMode)
            {
                if (!((VC.SpeedMode == 0) || ((VC.SpeedMode == 3) && !TargetExists())))
                {
                    FlightGlobals.SetSpeedMode((FlightGlobals.SpeedDisplayModes)(VC.SpeedMode - 1));
                }
                VCOld.SpeedMode = VC.SpeedMode;
            }

            if (Math.Abs(VC.Pitch) > Config.SASTol || Math.Abs(VC.Roll) > Config.SASTol || Math.Abs(VC.Yaw) > Config.SASTol)
            {
                if ((AV.ActionGroups[KSPActionGroup.SAS]) && (wasSASOn == false))
                {
                    wasSASOn = true;
                    lastSASMode = GetSASMode();
                    AV.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                }
            }
            else
            {
                if (wasSASOn == true)
                {
                    wasSASOn = false;
                    AV.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                    forceSASMode = true;
                    //SetSASMode(lastSASMode);
                }
            }

            if (VC.holdTargetVector)
            {
                Quaternion relativeOrientation = Quaternion.identity * Quaternion.Euler((-VC.targetHeading + 90) * new Vector3(1, 0, 0));
                relativeOrientation = relativeOrientation * Quaternion.Euler((VC.targetPitch) * new Vector3(0, 0, 1));
                relativeOrientation = relativeOrientation * Quaternion.Euler((VC.targetRoll + 90) * new Vector3(0, 1, 0));

                Quaternion goalOrientation = Quaternion.LookRotation(north, east) * relativeOrientation;

                Quaternion currentOrientation = FlightGlobals.ActiveVessel.Autopilot.SAS.lockedRotation;
                float delta = Quaternion.Angle(goalOrientation, currentOrientation);
                // float slerp = (float)Math.Pow(delta / 90f, 4) * 0.02f;
                float slerp = delta / 90f * 0.02f;
                FlightGlobals.ActiveVessel.Autopilot.SAS.LockRotation(Quaternion.Slerp(currentOrientation, goalOrientation, slerp));
            }
        }

        private void ServerReceive()
        {
            byte[] msg = new byte[Marshal.SizeOf(typeof(ControlPacket))];
            while (ns.DataAvailable)
            {
                ns.Read(msg, 0, msg.Length);

                ControlPacket str = new ControlPacket();
                int size = Marshal.SizeOf(str);
                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(msg, 0, ptr, size);
                str = (ControlPacket)Marshal.PtrToStructure(ptr, str.GetType());
                Marshal.FreeHGlobal(ptr);

                if (str.HEADER_0 == 0xC4)
                {
                    newDataRec = true;
                    VC = CPToVC(str);
                }
                else
                {
                    Debug.Log("YARK: server recieved malformed packet");
                }
            }
        }

        VesselControls CPToVC(ControlPacket cp)
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

                Pitch = (float)cp.Pitch / 1000.0F,
                Roll = (float)cp.Roll / 1000.0F,
                Yaw = (float)cp.Yaw / 1000.0F,
                TX = (float)cp.TX / 1000.0F,
                TY = (float)cp.TY / 1000.0F,
                TZ = (float)cp.TZ / 1000.0F,
                WheelSteer = (float)cp.WheelSteer / 1000.0F,
                Throttle = (float)cp.Throttle / 1000.0F,
                WheelThrottle = (float)cp.WheelThrottle / 1000.0F,

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

        #region structsNStuff

        byte[] Header_Array = new[] { (byte)0xFF, (byte)0xC4, (byte)'Y', (byte)'A', (byte)'R', (byte)'K', (byte)0x00, (byte)0xFF };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct Header
        {
            public fixed byte header[8];
        }


        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct StatusChange
        {
            public Header header;
            public byte packetType;
            public long ID;
            public byte status;
            public fixed byte vessalName[16]; //16 bytes
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
        public unsafe struct KSPData
        {
            //##### HEADER ######
            public Header header;
            public byte packetType;
            public long ID;

            public float deltaTime;

            //##### CRAFT ######
            public float Pitch; //pitch and heading close together so c++ can use this as a NavHeading ptr
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


            //###### ORBITAL ######
            public float VOrbit;
            public float AP;
            public float PE;
            public int TAp;
            public int TPe;
            public float SemiMajorAxis;
            public float SemiMinorAxis;
            public float e;
            public float inc;
            public int period;
            public float TrueAnomaly;
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
            public UInt32 MissionTime;
            public UInt32 MNTime;
            public float MNDeltaV;
            public byte HasTarget;
            public float TargetDist;    //  Distance to targeted vessel (m)
            public float TargetV;       //  Target vessel relative velocity (m/s)
            public byte SOINumber;      //  SOI Number (decimal format: sun-planet-moon e.g. 130 = kerbin, 131 = mun)

            public byte SASMode; //hold, prograde, retro, etc...
            public byte SpeedMode; //Surface, orbit target

            public byte timeWarpRateIndex;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ControlPacket
        {
            public byte HEADER_0;
            public long ID;
            public byte MainControls;                   //SAS RCS Lights Gear Brakes Abort Stage
            public UInt16 ActionGroups;                //action groups 1-10 in 2 bytes
            public short Pitch;                        //-1000 -> 1000
            public short Roll;                         //-1000 -> 1000
            public short Yaw;                          //-1000 -> 1000
            public short TX;                           //-1000 -> 1000
            public short TY;                           //-1000 -> 1000
            public short TZ;                           //-1000 -> 1000
            public short WheelSteer;                   //-1000 -> 1000
            public short Throttle;                     // 0 -> 1000
            public short WheelThrottle;                // 0 -> 1000
            public byte SASMode; //hold, prograde, retro, etc...
            public byte SpeedMode; //Surface, orbit target
            public float targetHeading, targetPitch, targetRoll;
            public byte timeWarpRateIndex;
        };

        public struct VesselControls
        {
            public Boolean SAS;
            public Boolean RCS;
            public Boolean Lights;
            public Boolean Gear;
            public Boolean Brakes;
            //public Boolean Precision;
            public Boolean Abort;
            public Boolean Stage;
            public int Mode;
            public int SASMode;
            public int SpeedMode;
            public Boolean[] ActionGroups;
            public float Pitch;
            public float Roll;
            public float Yaw;
            public float TX;
            public float TY;
            public float TZ;
            public float WheelSteer;
            public float Throttle;
            public float WheelThrottle;
            public float targetHeading, targetPitch, targetRoll;
            public Boolean holdTargetVector;
            public byte timeWarpRateIndex;
            public VesselControls(bool ignore)
            {
                holdTargetVector = SAS = RCS = Lights = Gear = Brakes /*= Precision*/ = Abort = Stage = false;
                Mode = SASMode = SpeedMode = 0;
                timeWarpRateIndex = 1;
                ActionGroups = new Boolean[10];
                targetHeading = targetPitch = targetRoll = Pitch = Roll = Yaw = TX = TY = TZ = WheelSteer = Throttle = WheelThrottle = 0;
            }
        };

        public struct IOResource
        {
            public float Max;
            public float Current;
        }
        #endregion

        #region UtilityFunction

        private byte GetTimeWarpIndex()
        {
            byte timeWarpRateIndex = (byte)TimeWarp.CurrentRateIndex;
            if (timeWarpRateIndex != 0 && TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
            {
                timeWarpRateIndex += 3;
            }
            return timeWarpRateIndex;
        }

        private void msg(string msg)
        {
            ScreenMessages.PostScreenMessage(msg);
            Debug.Log(msg);
        }

        private byte GetSASMode()
        {
            return (AV.ActionGroups[KSPActionGroup.SAS]) ? ((byte)(FlightGlobals.ActiveVessel.Autopilot.Mode + 1)) : (byte)(0);
        }

        private void SetSASMode(int mode)
        {
            if (AV.Autopilot.CanSetMode((VesselAutopilot.AutopilotMode)(mode - 1)))
            {
                AV.Autopilot.SetMode((VesselAutopilot.AutopilotMode)(mode - 1));
            }
            else
            {
                msg("SAS mode " + mode.ToString() + " not avalible");
            }
        }

        private static NavHeading WorldVecToNavHeading(Vector3d up, Vector3d north, Vector3d east, Vector3d v)
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

        private IOResource GetResourceTotal(Vessel V, string resourceName)
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

        /* private Boolean BitMathByte(byte x, int n)
         {
             return ((x >> n) & 1) == 1;
         }

         private Boolean BitMathshort(short x, int n)
         {
             return ((x >> n) & 1) == 1;
         }*/

        private Boolean TargetExists()
        {
            return (FlightGlobals.fetch.VesselTarget != null) && (FlightGlobals.fetch.VesselTarget.GetVessel() != null); //&& is short circuiting
        }

        private void AxisInput(FlightCtrlState s)
        {
            if (!conn)
            {
                return;
            }
            switch (Config.ThrottleEnable)
            {
                case 1:
                    s.mainThrottle = VC.Throttle;
                    break;
                case 2:
                    if (s.mainThrottle == 0)
                    {
                        s.mainThrottle = VC.Throttle;
                    }
                    break;
                case 3:
                    if (VC.Throttle != 0)
                    {
                        s.mainThrottle = VC.Throttle;
                    }
                    break;
                default:
                    break;
            }

            if (!VC.holdTargetVector)
            {
                switch (Config.PitchEnable)
                {
                    case 1:
                        s.pitch = VC.Pitch;
                        break;
                    case 2:
                        if (s.pitch == 0)
                            s.pitch = VC.Pitch;
                        break;
                    case 3:
                        if (VC.Pitch != 0)
                            s.pitch = VC.Pitch;
                        break;
                    default:
                        break;
                }

                switch (Config.RollEnable)
                {
                    case 1:
                        s.roll = VC.Roll;
                        break;
                    case 2:
                        if (s.roll == 0)
                            s.roll = VC.Roll;
                        break;
                    case 3:
                        if (VC.Roll != 0)
                            s.roll = VC.Roll;
                        break;
                    default:
                        break;
                }

                switch (Config.YawEnable)
                {
                    case 1:
                        s.yaw = VC.Yaw;
                        break;
                    case 2:
                        if (s.yaw == 0)
                            s.yaw = VC.Yaw;
                        break;
                    case 3:
                        if (VC.Yaw != 0)
                            s.yaw = VC.Yaw;
                        break;
                    default:
                        break;
                }
            }

            switch (Config.TXEnable)
            {
                case 1:
                    s.X = VC.TX;
                    break;
                case 2:
                    if (s.X == 0)
                        s.X = VC.TX;
                    break;
                case 3:
                    if (VC.TX != 0)
                        s.X = VC.TX;
                    break;
                default:
                    break;
            }

            switch (Config.TYEnable)
            {
                case 1:
                    s.Y = VC.TY;
                    break;
                case 2:
                    if (s.Y == 0)
                        s.Y = VC.TY;
                    break;
                case 3:
                    if (VC.TY != 0)
                        s.Y = VC.TY;
                    break;
                default:
                    break;
            }

            switch (Config.TZEnable)
            {
                case 1:
                    s.Z = VC.TZ;
                    break;
                case 2:
                    if (s.Z == 0)
                        s.Z = VC.TZ;
                    break;
                case 3:
                    if (VC.TZ != 0)
                        s.Z = VC.TZ;
                    break;
                default:
                    break;
            }

            switch (Config.WheelSteerEnable)
            {
                case 1:
                    s.wheelSteer = VC.WheelSteer;
                    break;
                case 2:
                    if (s.wheelSteer == 0)
                    {
                        s.wheelSteer = VC.WheelSteer;
                    }
                    break;
                case 3:
                    if (VC.WheelSteer != 0)
                    {
                        s.wheelSteer = VC.WheelSteer;
                    }
                    break;
                default:
                    break;
            }

            switch (Config.WheelThrottleEnable)
            {
                case 1:
                    s.wheelThrottle = VC.WheelThrottle;
                    break;
                case 2:
                    if (s.wheelThrottle == 0)
                    {
                        s.wheelThrottle = VC.WheelThrottle;
                    }
                    break;
                case 3:
                    if (VC.WheelThrottle != 0)
                    {
                        s.wheelThrottle = VC.WheelThrottle;
                    }
                    break;
                default:
                    break;
            }
        }

        private byte GetSOINumber(string name)
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

        private byte GetMaxOverHeat(Vessel V)
        {
            byte percent = 0;
            double sPercent = 0, iPercent = 0;
            double percentD = 0, percentP = 0;

            foreach (Part p in AV.parts)
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

        #endregion
    }
}