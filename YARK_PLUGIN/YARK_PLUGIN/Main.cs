using System;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Net;  
using System.Net.Sockets;    
using UnityEngine.SceneManagement;
using KSP;
using System.Collections.Generic;

namespace KSP_YARK
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class YARK : MonoBehaviour
    {
        TcpListener server;
        TcpClient client;
        NetworkStream ns;
        bool conn;
        KSPData KD;
        public static Vessel ActiveVessel;
        IOResource TempR;

        public void Awake()
        {
            TempR = new IOResource();
            DontDestroyOnLoad(gameObject);
            Debug.Log("GO");
            ActiveVessel = new Vessel();
            KD = new KSPData();
            KD.ID = 0;
            KD.HEADER_0 = 0xDE;
            KD.HEADER_1 = 0xAD;
            server = new TcpListener(IPAddress.Any, 9999);
            server.Start();
            conn = false;
        }
        public void Update()
        {
            if (conn)
            {
                if (!client.Connected)
                {
                    Debug.Log("Client disconnected");
                    conn = false;
                }
                if (SceneManager.GetActiveScene().buildIndex == 7) //flight
                {
                    KD.status = 1;
                    //If the current active vessel is not what we were using, we need to remove controls from the old 
                    //vessel and attache it to the current one
                    if (ActiveVessel.id != FlightGlobals.ActiveVessel.id)
                    {
                        //ActiveVessel.OnPostAutopilotUpdate -= AxisInput; //defaq does this do?
                        ActiveVessel = FlightGlobals.ActiveVessel;
                        //ActiveVessel.OnPostAutopilotUpdate += AxisInput; //defaq does this do
                        //sync some inputs on vessel switch
                        //ActiveVessel.ActionGroups.SetGroup(KSPActionGroup.RCS, KSPSerialPort.VControls.RCS);
                        //ActiveVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, KSPSerialPort.VControls.SAS);
                        Debug.Log("KSPIO: ActiveVessel changed");
                    }
                    else
                    {
                        ActiveVessel = FlightGlobals.ActiveVessel;
                    }

                    List<Part> ActiveEngines = new List<Part>();
                    ActiveEngines = GetListOfActivatedEngines(ActiveVessel);

                    KD.Lat = (float)ActiveVessel.latitude;
                    KD.Lon = (float)ActiveVessel.longitude;

                    TempR = GetResourceTotal(ActiveVessel, "LiquidFuel");
                    KD.LiquidFuelTot = TempR.Max;
                    KD.LiquidFuel = TempR.Current;

                    KD.LiquidFuelTotS = (float)ProspectForResourceMax("LiquidFuel", ActiveEngines);
                    KD.LiquidFuelS = (float)ProspectForResource("LiquidFuel", ActiveEngines);

                    TempR = GetResourceTotal(ActiveVessel, "Oxidizer");
                    KD.OxidizerTot = TempR.Max;
                    KD.Oxidizer = TempR.Current;

                    KD.OxidizerTotS = (float)ProspectForResourceMax("Oxidizer", ActiveEngines);
                    KD.OxidizerS = (float)ProspectForResource("Oxidizer", ActiveEngines);

                    TempR = GetResourceTotal(ActiveVessel, "ElectricCharge");
                    KD.EChargeTot = TempR.Max;
                    KD.ECharge = TempR.Current;
                    TempR = GetResourceTotal(ActiveVessel, "MonoPropellant");
                    KD.MonoPropTot = TempR.Max;
                    KD.MonoProp = TempR.Current;
                    TempR = GetResourceTotal(ActiveVessel, "IntakeAir");
                    KD.IntakeAirTot = TempR.Max;
                    KD.IntakeAir = TempR.Current;
                    TempR = GetResourceTotal(ActiveVessel, "SolidFuel");
                    KD.SolidFuelTot = TempR.Max;
                    KD.SolidFuel = TempR.Current;
                    TempR = GetResourceTotal(ActiveVessel, "XenonGas");
                    KD.XenonGasTot = TempR.Max;
                    KD.XenonGas = TempR.Current;


                    Quaternion attitude = updateHeadingPitchRollField(ActiveVessel);
                    KD.Roll = (float)((attitude.eulerAngles.z > 180) ? (attitude.eulerAngles.z - 360.0) : attitude.eulerAngles.z);
                    KD.Pitch = (float)((attitude.eulerAngles.x > 180) ? (360.0 - attitude.eulerAngles.x) : -attitude.eulerAngles.x);
                    KD.Heading = (float)attitude.eulerAngles.y;
                }
                else
                {
                    KD.status = 0;
                }
                KD.ID++;
                //Debug.Log("ID: " + KD.ID + "Status: " + KD.status);
                SendData();
            }
            ServerReceive();
        }
        public void OnDisable()
        {
            Debug.Log("STOP");
        }

        private void SendData()
        {
            int size = Marshal.SizeOf(KD);
            byte[] arr = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(KD, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            Debug.Log(arr[0] + " : " + arr[1] + " : " + arr[2] + " : " + arr[3] + " : " + arr[4] + " : " + arr[5] + " : " + arr[6] + " : " + arr[7] + " : " + arr[8] + " : " + arr[9] + " : ");

            ns.Write(arr, 0, arr.Length);

        }

        private void ServerReceive()
        {

            if (conn)
            {
                byte[] msg = new byte[256];
                while (ns.DataAvailable)
                {
                    ns.Read(msg, 0, msg.Length);
                    Debug.Log("data recieved");
                    if (msg[0] == 0xDE && msg[1] == 0xAD)
                    {

                    }
                    else
                    {
                        //bad
                    }

                    //Debug.Log(Encoding.Default.GetString(msg));
                }
            }
            else
            {
                if (server.Pending())
                {
                    Debug.Log("Client connected");
                    client = server.AcceptTcpClient();  //if a connection exists, the server will accept it
                    ns = client.GetStream(); //networkstream is used to send/receive messages
                    conn = true;
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct KSPData
        {
            public byte HEADER_0;
            public byte HEADER_1;
            public int ID;
            public byte status; //0 = 
            public float Roll;
            public float Pitch;
            public float Heading;
            public float Lat;
            public float Lon;
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
        }

        public struct IOResource
        {
            public float Max;
            public float Current;
        }

        #region UtilityFunction

        private Quaternion updateHeadingPitchRollField(Vessel v)
        {
            Vector3d CoM, north, up;
            Quaternion rotationSurface;
            CoM = v.CoM;
            up = (CoM - v.mainBody.position).normalized;
            north = Vector3d.Exclude(up, (v.mainBody.position + v.mainBody.transform.up * (float)v.mainBody.Radius) - CoM).normalized;
            rotationSurface = Quaternion.LookRotation(north, up);
            return Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(v.GetTransform().rotation) * rotationSurface);
        }

        // this recursive stage look up stuff stolen and modified from KOS and others
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
        }

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
    }
    #endregion
}