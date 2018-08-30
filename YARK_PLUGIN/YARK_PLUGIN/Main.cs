using System;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using UnityEngine.SceneManagement;
using KSP;
using System.Collections.Generic;
using KSP.UI.Screens;
using KSP.UI.Screens.Flight;

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
        bool inFlight;
        public static Vessel AV;
        IOResource TempR;
        Boolean wasSASOn = false;
        float TimeOFLastSend;

        public void Awake()
        {
            TempR = new IOResource();
            DontDestroyOnLoad(gameObject);
            Debug.Log("GO");
            AV = new Vessel();
            SC = new StatusChange();
            SC.HEADER_0 = 0xC4;
            SC.packetType = 0x01;
            SC.ID = 0;

            KD = new KSPData();
            KD.HEADER_0 = 0xC4;
            KD.packetType = 0x02;
            KD.ID = 0;

            VC = new VesselControls(false);
            VCOld = new VesselControls(false);

            server = new TcpListener(IPAddress.Any, Config.TCPPort);
            server.Start();
            conn = false;
        }
        public void Update()
        {
            if (conn)
            {
                if (!client.Connected)
                {
                                        AV.OnPostAutopilotUpdate -= AxisInput;
                    Debug.Log("Client disconnected");
                    conn = false;
                }
                else
                {
                    if (SceneManager.GetActiveScene().buildIndex == 7)
                    {
                        //If the current active vessel is not what we were using, we need to remove controls from the old 
                        //vessel and attache it to the current one
                        if (AV.id != FlightGlobals.ActiveVessel.id)
                        {
                            AV.OnPostAutopilotUpdate -= AxisInput;
                            AV = FlightGlobals.ActiveVessel;
                            AV.OnPostAutopilotUpdate += AxisInput;
                            //sync some inputs on vessel switch

                            // AV.ActionGroups.SetGroup(KSPActionGroup.RCS, VC.RCS);
                            //AV.ActionGroups.SetGroup(KSPActionGroup.SAS, VC.SAS);

                            Debug.Log("KSPIO: ActiveVessel changed");
                            inFlight = true; //dont send two statusUpdate packets
                            SendNewStatus();
                        }
                        else
                        {
                            AV = FlightGlobals.ActiveVessel;
                        }
                        SendKD();
                    }
                    else
                    {
                        if (inFlight)
                        {
                            Debug.Log("left flight");
                            inFlight = false;
                            SendNewStatus();
                            AV = new Vessel();
                        }
                    }
                    ServerReceive();
                }
            }
            else if (server.Pending())
            {
                Debug.Log("Client connected");
                AV = new Vessel();
                VC = new VesselControls(false);
                VCOld = new VesselControls(false);

                client = server.AcceptTcpClient();  //if a connection exists, the server will accept it
                ns = client.GetStream(); //networkstream is used to send/receive messages
                conn = true;
                bool wasFlight = SceneManager.GetActiveScene().buildIndex == 7;
                if (!wasFlight)
                {
                    SendNewStatus();
                }
                TimeOFLastSend = 0;
            }
        }
        public void OnDisable()
        {
            Debug.Log("STOP");
        }
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
            TimeOFLastSend = time;
            if (Config.UpdatesPerSecond != 0 && ((KD.deltaTime) < (1.0f / (float)(Config.UpdatesPerSecond))))
            {
                return;
            }

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

            //Debug.Log("KSPSerialIO: 3");
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
            Vector3d CoM, north, up, east;
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

            ControlStatus((int)ActionGroups.SAS, AV.ActionGroups[KSPActionGroup.SAS]);
            ControlStatus((int)ActionGroups.RCS, AV.ActionGroups[KSPActionGroup.RCS]);
            ControlStatus((int)ActionGroups.Light, AV.ActionGroups[KSPActionGroup.Light]);
            ControlStatus((int)ActionGroups.Gear, AV.ActionGroups[KSPActionGroup.Gear]);
            ControlStatus((int)ActionGroups.Brakes, AV.ActionGroups[KSPActionGroup.Brakes]);
            ControlStatus((int)ActionGroups.Abort, AV.ActionGroups[KSPActionGroup.Abort]);
            ControlStatus((int)ActionGroups.Custom01, AV.ActionGroups[KSPActionGroup.Custom01]);
            ControlStatus((int)ActionGroups.Custom02, AV.ActionGroups[KSPActionGroup.Custom02]);
            ControlStatus((int)ActionGroups.Custom03, AV.ActionGroups[KSPActionGroup.Custom03]);
            ControlStatus((int)ActionGroups.Custom04, AV.ActionGroups[KSPActionGroup.Custom04]);
            ControlStatus((int)ActionGroups.Custom05, AV.ActionGroups[KSPActionGroup.Custom05]);
            ControlStatus((int)ActionGroups.Custom06, AV.ActionGroups[KSPActionGroup.Custom06]);
            ControlStatus((int)ActionGroups.Custom07, AV.ActionGroups[KSPActionGroup.Custom07]);
            ControlStatus((int)ActionGroups.Custom08, AV.ActionGroups[KSPActionGroup.Custom08]);
            ControlStatus((int)ActionGroups.Custom09, AV.ActionGroups[KSPActionGroup.Custom09]);
            ControlStatus((int)ActionGroups.Custom10, AV.ActionGroups[KSPActionGroup.Custom10]);

            if (AV.orbit.referenceBody != null)
            {
                KD.SOINumber = GetSOINumber(AV.orbit.referenceBody.name);
            }

            KD.MaxOverHeat = GetMaxOverHeat(AV);
            KD.IAS = (float)AV.indicatedAirSpeed;

            KD.CurrentStage = (byte)StageManager.CurrentStage;
            KD.TotalStage = (byte)StageManager.StageCount;

            KD.SpeedMode = (byte)(FlightGlobals.speedDisplayMode + 1);
            KD.SASMode = (AV.ActionGroups[KSPActionGroup.SAS]) ? ((byte)(FlightGlobals.ActiveVessel.Autopilot.Mode + 1)) : (byte)(0);

            KD.ID++;

            int size = Marshal.SizeOf(KD);
            byte[] arr = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(KD, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            ns.Write(arr, 0, arr.Length);

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

            if (VC.ControlGroup[1] != VCOld.ControlGroup[1])
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Custom01, VC.ControlGroup[1]);
                VCOld.ControlGroup[1] = VC.ControlGroup[1];
            }

            if (VC.ControlGroup[2] != VCOld.ControlGroup[2])
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Custom02, VC.ControlGroup[2]);
                VCOld.ControlGroup[2] = VC.ControlGroup[2];
            }

            if (VC.ControlGroup[3] != VCOld.ControlGroup[3])
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Custom03, VC.ControlGroup[3]);
                VCOld.ControlGroup[3] = VC.ControlGroup[3];
            }

            if (VC.ControlGroup[4] != VCOld.ControlGroup[4])
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Custom04, VC.ControlGroup[4]);
                VCOld.ControlGroup[4] = VC.ControlGroup[4];
            }

            if (VC.ControlGroup[5] != VCOld.ControlGroup[5])
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Custom05, VC.ControlGroup[5]);
                VCOld.ControlGroup[5] = VC.ControlGroup[5];
            }

            if (VC.ControlGroup[6] != VCOld.ControlGroup[6])
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Custom06, VC.ControlGroup[6]);
                VCOld.ControlGroup[6] = VC.ControlGroup[6];
            }

            if (VC.ControlGroup[7] != VCOld.ControlGroup[7])
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Custom07, VC.ControlGroup[7]);
                VCOld.ControlGroup[7] = VC.ControlGroup[7];
            }

            if (VC.ControlGroup[8] != VCOld.ControlGroup[8])
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Custom08, VC.ControlGroup[8]);
                VCOld.ControlGroup[8] = VC.ControlGroup[8];
            }

            if (VC.ControlGroup[9] != VCOld.ControlGroup[9])
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Custom09, VC.ControlGroup[9]);
                VCOld.ControlGroup[9] = VC.ControlGroup[9];
            }

            if (VC.ControlGroup[10] != VCOld.ControlGroup[10])
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Custom10, VC.ControlGroup[10]);
                VCOld.ControlGroup[10] = VC.ControlGroup[10];
            }

            //Set sas mode
            if (VC.SASMode != VCOld.SASMode)
            {
                if (VC.SASMode != 0 && VC.SASMode < 11)
                {
                    if (!AV.Autopilot.CanSetMode((VesselAutopilot.AutopilotMode)(VC.SASMode - 1)))
                    {
                        ScreenMessages.PostScreenMessage("SAS mode " + VC.SASMode.ToString() + " not avalible");
                    }
                    else
                    {
                        AV.Autopilot.SetMode((VesselAutopilot.AutopilotMode)VC.SASMode - 1);
                    }
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

            if (Math.Abs(VC.Pitch) > Config.SASTol ||
Math.Abs(VC.Roll) > Config.SASTol ||
Math.Abs(VC.Yaw) > Config.SASTol)
            {
                if ((AV.ActionGroups[KSPActionGroup.SAS]) && (wasSASOn == false))
                {
                    wasSASOn = true;
                    AV.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                }
            }
            else
            {
                if (wasSASOn == true)
                {
                    wasSASOn = false;
                    AV.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                }
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

                VC.SAS = BitMathByte(str.MainControls, 7);
                VC.RCS = BitMathByte(str.MainControls, 6);
                VC.Lights = BitMathByte(str.MainControls, 5);
                VC.Gear = BitMathByte(str.MainControls, 4);
                VC.Brakes = BitMathByte(str.MainControls, 3);
                VC.Precision = BitMathByte(str.MainControls, 2);
                VC.Abort = BitMathByte(str.MainControls, 1);
                VC.Stage = BitMathByte(str.MainControls, 0);

                VC.Pitch = (float)str.Pitch / 1000.0F;
                VC.Roll = (float)str.Roll / 1000.0F;
                VC.Yaw = (float)str.Yaw / 1000.0F;
                VC.TX = (float)str.TX / 1000.0F;
                VC.TY = (float)str.TY / 1000.0F;
                VC.TZ = (float)str.TZ / 1000.0F;
                VC.WheelSteer = (float)str.WheelSteer / 1000.0F;
                VC.Throttle = (float)str.Throttle / 1000.0F;
                VC.WheelThrottle = (float)str.WheelThrottle / 1000.0F;
                VC.SASMode = (int)str.SASMode;
                VC.SpeedMode = (int)str.SpeedMode;

                //Debug.Log("main ctrs: " + str.MainControls);

                for (int j = 1; j <= 10; j++)
                {
                    VC.ControlGroup[j] = BitMathshort(str.ControlGroup, j);
                }
            }
            UpdateControls();
        }

        #region structsNStuff
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct StatusChange
        {
            public byte HEADER_0;
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
        public struct KSPData
        {
            //##### HEADER ######
            public byte HEADER_0;
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

            public UInt16 ActionGroups; //  status bit order:SAS, RCS, Light, Gear, Brakes, Abort, Custom01 - 10 
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
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ControlPacket
        {
            public byte HEADER_0;
            public long ID;
            public byte MainControls;                  //SAS RCS Lights Gear Brakes Precision Abort Stage 
            public byte Mode;                          //0 = stage, 1 = docking, 2 = map
            public short ControlGroup;                //control groups 1-10 in 2 bytes
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
        };

        public struct VesselControls
        {
            public Boolean SAS;
            public Boolean RCS;
            public Boolean Lights;
            public Boolean Gear;
            public Boolean Brakes;
            public Boolean Precision;
            public Boolean Abort;
            public Boolean Stage;
            public int Mode;
            public int SASMode;
            public int SpeedMode;
            public Boolean[] ControlGroup;
            public float Pitch;
            public float Roll;
            public float Yaw;
            public float TX;
            public float TY;
            public float TZ;
            public float WheelSteer;
            public float Throttle;
            public float WheelThrottle;
            public VesselControls(bool ignore)
            {
                SAS = RCS = Lights = Gear = Brakes = Precision = Abort = Stage = false;
                Mode = SASMode = SpeedMode = 0;
                ControlGroup = new Boolean[11];
                Pitch = Roll = Yaw = TX = TY = TZ = WheelSteer = Throttle = WheelThrottle = 0;
            }
        };

        enum ActionGroups : int
        {
            SAS,
            RCS,
            Light,
            Gear,
            Brakes,
            Abort,
            Custom01,
            Custom02,
            Custom03,
            Custom04,
            Custom05,
            Custom06,
            Custom07,
            Custom08,
            Custom09,
            Custom10,
        };

        public struct IOResource
        {
            public float Max;
            public float Current;
        }
        #endregion
        #region UtilityFunction

        private static NavHeading WorldVecToNavHeading(Vector3d up, Vector3d north, Vector3d east, Vector3d v)
        {
            NavHeading ret = new NavHeading();
            ret.Pitch = (float)-((Vector3d.Angle(up, v)) - 90.0f);
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

        private Boolean BitMathByte(byte x, int n)
        {
            return ((x >> n) & 1) == 1;
        }

        private Boolean BitMathshort(short x, int n)
        {
            return ((x >> n) & 1) == 1;
        }

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
            /*
            if (ActiveVessel.Autopilot.SAS.lockedMode == true)
            {
            }
            */
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

        private void ControlStatus(int n, bool s)
        {
            if (s)
                KD.ActionGroups |= (UInt16)(1 << n);       // forces nth bit of x to be 1.  all other bits left alone.
            else
                KD.ActionGroups &= (UInt16)~(1 << n);      // forces nth bit of x to be 0.  all other bits left alone.
        }

        #endregion
    }


}