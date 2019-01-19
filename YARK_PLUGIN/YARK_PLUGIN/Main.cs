using System;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using KSP.UI.Screens;
using KSP_PLUGIN;
using static KSP_PLUGIN.Structs;
using static KSP_PLUGIN.Util;

namespace KSP_YARK
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class Main : MonoBehaviour
    {
        //Connection con;
        public static Connection conn;

        TcpListener server;

        VesselPacket VP;
        StatusPacket SP;

        // ControlPacket CP = new ControlPacket();

        int inFlight;
        public static Vessel AV;
        IOResource TempR;

        private Vector3d CoM, north, up, east;
        private Boolean wasSASOn = false, forceSASMode = false;
        private int lastSASMode = 1;
        private float TimeOFLastSend;
        private UInt32 currentTime, lastTime; //Checking for revert / save backwards in time

        public void Awake()
        {
            TempR = new IOResource();
            DontDestroyOnLoad(gameObject);
            msg("Starting YARK KSPWebsockIO");

            VP = new VesselPacket();

            server = new TcpListener(IPAddress.Any, Config.TCPPort);
            server.Start();

            inFlight = -1;
        }

        public void Update()
        {
            if (conn != null)
            {
                if (!conn.GetConnected())
                {
                    msg("YARK: client disconneted");
                    conn = null;
                }
                else
                {
                    if (SceneManager.GetActiveScene().buildIndex == 7) //in flight?
                    {
                        currentTime = (UInt32)Planetarium.GetUniversalTime();
                        if (inFlight != 1 || AV.id != FlightGlobals.ActiveVessel.id || lastTime > currentTime)
                        {
                            inFlight = 1;
                            if (AV != null)
                            {
                                AV.OnPostAutopilotUpdate -= AxisInput.Callback;
                            }
                            AV = FlightGlobals.ActiveVessel;
                            AV.OnPostAutopilotUpdate += AxisInput.Callback;

                            //sync inputs on vessel switch
                            ControlPacket cp = new ControlPacket
                            {
                                MainControls = CalcMainControls(),
                                ActionGroups = CalcActionGroups(),
                                SASMode = GetSASMode(),
                                SpeedMode = (byte)(FlightGlobals.speedDisplayMode + 1),
                                timeWarpRateIndex = GetTimeWarpIndex()
                            };
                            cp.targetHeading = cp.targetPitch = cp.targetRoll = cp.WheelSteer = cp.WheelThrottle = cp.Throttle = cp.Pitch = cp.Roll = cp.Yaw = cp.TX = cp.TY = cp.TZ = 0;

                            conn.SyncControls(CPToVC(cp));

                            msg("rysync");

                            UpdateSP();
                            conn.SendStatusPacket(SP);
                        }
                        lastTime = currentTime;
                        if (forceSASMode)
                        {
                            forceSASMode = false;
                            SetSASMode(lastSASMode);
                        }

                        if (AxisInput.SupressSAS) // SAS suspention for joystick input
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
                            }
                        }

                        if (AxisInput.holdTargetVector) //custom SAS vectoring WIP
                        {
                            Quaternion relativeOrientation = Quaternion.identity * Quaternion.Euler((-AxisInput.targetHeading + 90) * new Vector3(1, 0, 0));
                            relativeOrientation = relativeOrientation * Quaternion.Euler((AxisInput.targetPitch) * new Vector3(0, 0, 1));
                            relativeOrientation = relativeOrientation * Quaternion.Euler((AxisInput.targetRoll + 90) * new Vector3(0, 1, 0));

                            Quaternion goalOrientation = Quaternion.LookRotation(north, east) * relativeOrientation;

                            Quaternion currentOrientation = FlightGlobals.ActiveVessel.Autopilot.SAS.lockedRotation;
                            float delta = Quaternion.Angle(goalOrientation, currentOrientation);
                            // float slerp = (float)Math.Pow(delta / 90f, 4) * 0.02f;
                            float slerp = delta / 90f * 0.02f;
                            FlightGlobals.ActiveVessel.Autopilot.SAS.LockRotation(Quaternion.Slerp(currentOrientation, goalOrientation, slerp));
                        }

                        float time = Time.unscaledTime;
                        VP.deltaTime = time - TimeOFLastSend;
                        if (Config.UpdatesPerSecond == 0 || ((VP.deltaTime) > (1.0f / (float)(Config.UpdatesPerSecond)))) //Limit send rate to config rate 
                        {
                            TimeOFLastSend = time;
                            UpdateVP();
                            conn.SendVesselPacket(VP);
                            if (conn != null)
                            {
                                while (conn.HavePackets())
                                {
                                    VCDifference vcDiff = conn.GetVC();
                                    UpdateControls(vcDiff.oldVC, vcDiff.newVC);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (inFlight != 0)
                        {
                            msg("switched to NOT FLYING");
                            inFlight = 0;
                            UpdateSP();
                            conn.SendStatusPacket(SP);
                        }
                        if (conn != null)
                        {
                            while (conn.HavePackets())
                            {
                                conn.GetVC(); //ignore results
                            }
                        }
                    }
                }
            }
            else if (server.Pending())
            {
                msg("YARK: Client connected");
                conn = new Connection(server.AcceptTcpClient());

                UpdateSP();
                conn.SendStatusPacket(SP);

                inFlight = SP.status;

                TimeOFLastSend = Time.unscaledTime;
            }
        }

        private void UpdateSP()
        {
            SP.status = inFlight == 1 ? (byte)1 : (byte)0;
            char[] name = inFlight == 1 ? AV.vesselName.ToCharArray() : "null".ToCharArray();
            unsafe
            {
                fixed (byte* charPtr = SP.vessalName)
                {
                    int i = 0;
                    for (; i < Math.Min(31, name.Length); i++)
                    {
                        *(charPtr + i) = (byte)name[i];
                    }
                    *(charPtr + i) = (byte)0x00;
                }
            }
        }

        private void UpdateVP()
        {
            List<Part> ActiveEngines = new List<Part>();
            ActiveEngines = GetListOfActivatedEngines(AV);

            VP.AP = (float)AV.orbit.ApA;
            VP.PE = (float)AV.orbit.PeA;
            VP.SemiMajorAxis = (float)AV.orbit.semiMajorAxis;
            VP.SemiMinorAxis = (float)AV.orbit.semiMinorAxis;
            VP.e = (float)AV.orbit.eccentricity;
            VP.inc = (float)AV.orbit.inclination;
            VP.VVI = (float)AV.verticalSpeed;
            VP.G = (float)AV.geeForce;
            VP.TAp = (int)Math.Round(AV.orbit.timeToAp);
            VP.TPe = (int)Math.Round(AV.orbit.timeToPe);
            VP.TrueAnomaly = (float)AV.orbit.trueAnomaly;
            VP.period = (int)Math.Round(AV.orbit.period);

            double ASL = AV.mainBody.GetAltitude(AV.CoM);
            double AGL = (ASL - AV.terrainAltitude);

            if (AGL < ASL)
                VP.RAlt = (float)AGL;
            else
                VP.RAlt = (float)ASL;

            VP.Alt = (float)ASL;
            VP.Vsurf = (float)AV.srfSpeed;
            VP.Lat = (float)AV.latitude;
            VP.Lon = (float)AV.longitude;

            TempR = GetResourceTotal(AV, "LiquidFuel");
            VP.LiquidFuelTot = TempR.Max;
            VP.LiquidFuel = TempR.Current;

            VP.LiquidFuelTotS = (float)ProspectForResourceMax("LiquidFuel", ActiveEngines);
            VP.LiquidFuelS = (float)ProspectForResource("LiquidFuel", ActiveEngines);

            TempR = GetResourceTotal(AV, "Oxidizer");
            VP.OxidizerTot = TempR.Max;
            VP.Oxidizer = TempR.Current;

            VP.OxidizerTotS = (float)ProspectForResourceMax("Oxidizer", ActiveEngines);
            VP.OxidizerS = (float)ProspectForResource("Oxidizer", ActiveEngines);

            TempR = GetResourceTotal(AV, "ElectricCharge");
            VP.EChargeTot = TempR.Max;
            VP.ECharge = TempR.Current;
            TempR = GetResourceTotal(AV, "MonoPropellant");
            VP.MonoPropTot = TempR.Max;
            VP.MonoProp = TempR.Current;
            TempR = GetResourceTotal(AV, "IntakeAir");
            VP.IntakeAirTot = TempR.Max;
            VP.IntakeAir = TempR.Current;
            TempR = GetResourceTotal(AV, "SolidFuel");
            VP.SolidFuelTot = TempR.Max;
            VP.SolidFuel = TempR.Current;
            TempR = GetResourceTotal(AV, "XenonGas");
            VP.XenonGasTot = TempR.Max;
            VP.XenonGas = TempR.Current;

            VP.MissionTime = (UInt32)Math.Round(AV.missionTime);

            VP.VOrbit = (float)AV.orbit.GetVel().magnitude;

            VP.MNTime = 0;
            VP.MNDeltaV = 0;
            VP.TargetDist = 0;
            VP.TargetV = 0;

            VP.HasTarget = TargetExists() ? (byte)1 : (byte)0;

            //mathy stuff
            Quaternion rotationSurface;
            CoM = AV.CoM;
            up = (CoM - AV.mainBody.position).normalized;
            north = Vector3d.Exclude(up, (AV.mainBody.position + AV.mainBody.transform.up * (float)AV.mainBody.Radius) - CoM).normalized;

            east = Vector3d.Cross(up, north);

            rotationSurface = Quaternion.LookRotation(north, up);

            Vector3d attitude = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(AV.GetTransform().rotation) * rotationSurface).eulerAngles;

            VP.Roll = (float)((attitude.z > 180) ? (attitude.z - 360.0) : attitude.z);
            VP.Pitch = (float)((attitude.x > 180) ? (360.0 - attitude.x) : -attitude.x);
            VP.Heading = (float)attitude.y;

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

            VP.Prograde = WorldVecToNavHeading(up, north, east, prograde);

            if (TargetExists())
            {
                VP.Target = WorldVecToNavHeading(up, north, east, AV.targetObject.GetTransform().position - AV.transform.position);
                VP.TargetDist = (float)Vector3.Distance(FlightGlobals.fetch.VesselTarget.GetVessel().transform.position, AV.transform.position);
                VP.TargetV = (float)FlightGlobals.ship_tgtVelocity.magnitude;

            }
            //vp.NormalHeading = WorldVecToNavHeading(up, north, east, Vector3d.Cross(AV.obt_velocity.normalized, up)).Heading;

            if (AV.patchedConicSolver != null)
            {
                if (AV.patchedConicSolver.maneuverNodes != null)
                {
                    if (AV.patchedConicSolver.maneuverNodes.Count > 0)
                    {
                        VP.MNTime = (UInt32)Math.Round(AV.patchedConicSolver.maneuverNodes[0].UT - Planetarium.GetUniversalTime());
                        VP.MNDeltaV = (float)AV.patchedConicSolver.maneuverNodes[0].GetBurnVector(AV.patchedConicSolver.maneuverNodes[0].patch).magnitude; //Added JS

                        VP.Maneuver = WorldVecToNavHeading(up, north, east, AV.patchedConicSolver.maneuverNodes[0].GetBurnVector(AV.patchedConicSolver.maneuverNodes[0].patch));
                    }
                }
            }

            VP.MainControls = CalcMainControls();

            VP.ActionGroups = CalcActionGroups();

            if (AV.orbit.referenceBody != null)
            {
                VP.SOINumber = GetSOINumber(AV.orbit.referenceBody.name);
            }

            VP.MaxOverHeat = GetMaxOverHeat(AV);
            VP.IAS = (float)AV.indicatedAirSpeed;

            VP.CurrentStage = (byte)StageManager.CurrentStage;
            VP.TotalStage = (byte)StageManager.StageCount;

            VP.SpeedMode = (byte)(FlightGlobals.speedDisplayMode + 1);
            VP.SASMode = GetSASMode();

            VP.timeWarpRateIndex = GetTimeWarpIndex();
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

        private void UpdateControls(VesselControls VCOld, VesselControls VC)
        {
            if (VC.RCS != VCOld.RCS)
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.RCS, VC.RCS);
            }
            if (VC.SAS != VCOld.SAS)
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.SAS, VC.SAS);
            }
            if (VC.Lights != VCOld.Lights)
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Light, VC.Lights);
            }
            if (VC.Gear != VCOld.Gear)
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Gear, VC.Gear);
            }
            if (VC.Brakes != VCOld.Brakes)
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Brakes, VC.Brakes);
            }
            if (VC.Abort != VCOld.Abort)
            {
                AV.ActionGroups.SetGroup(KSPActionGroup.Abort, VC.Abort);
            }
            if (VC.Stage != VCOld.Stage)
            {
                if (VC.Stage)
                    StageManager.ActivateNextStage();

                AV.ActionGroups.SetGroup(KSPActionGroup.Stage, VC.Stage);
            }

            //================ control groups

            for (int j = 0; j < 10; j++)
            {
                if (VC.ActionGroups[j] != VCOld.ActionGroups[j])
                {
                    AV.ActionGroups.SetGroup((KSPActionGroup)(1 << (7 + j)), VC.ActionGroups[1]);
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
            }

            //Set sas mode
            if (VC.SASMode != VCOld.SASMode)
            {
                int setTo = VC.SASMode;
                if (setTo == 11)
                {
                    setTo = 1;
                    AxisInput.holdTargetVector = true;
                }
                else
                {
                    AxisInput.holdTargetVector = false;
                }
                if (setTo != 0 && setTo < 11)
                {
                    SetSASMode(setTo);
                }
            }

            //set navball mode
            if (VC.SpeedMode != VCOld.SpeedMode)
            {
                if (!((VC.SpeedMode == 0) || ((VC.SpeedMode == 3) && !TargetExists())))
                {
                    FlightGlobals.SetSpeedMode((FlightGlobals.SpeedDisplayModes)(VC.SpeedMode - 1));
                }
            }

            if (!float.IsNaN(VC.targetHeading) && !float.IsNaN(VC.targetPitch) && !float.IsNaN(VC.targetRoll))
            {
                AxisInput.targetPitch = VC.targetPitch;
                AxisInput.targetRoll = VC.targetRoll;
                AxisInput.targetHeading = VC.targetHeading;
            }
        }

        public static byte GetSASMode()
        {
            return (AV.ActionGroups[KSPActionGroup.SAS]) ? ((byte)(FlightGlobals.ActiveVessel.Autopilot.Mode + 1)) : (byte)(0);
        }

        public static void SetSASMode(int mode)
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

        public Boolean TargetExists()
        {
            return (FlightGlobals.fetch.VesselTarget != null) && (FlightGlobals.fetch.VesselTarget.GetVessel() != null); //&& is short circuiting
        }

    }
}