using System;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Net.Sockets;
using static KSP_PLUGIN.Structs;
using static KSP_PLUGIN.Util;
using System.Threading;
using System.IO;
using System.Collections.Generic;

namespace KSP_PLUGIN
{
    public struct VCDifference
    {
        public VesselControls oldVC, newVC;
    };

    public class Connection
    {
        TcpClient client;

        Queue<VesselControls> VCList;
        Queue<ManChangePacket> MCPList;
        Queue<byte[]> sendQueue;
        NetworkStream ns;
        SendSP sendSP;
        SendVP sendVP;
        Header sendODPHeader;
        VesselControls VCOld;
        AxisControls axisControls;

        int SPc = 0, VPc = 0;

        int lastCPID = -1;

        bool Connected;

        public AxisControls GetAxisControls()
        {
            return axisControls;
        }

        public bool GetConnected()
        {
            return Connected;
        }

        public Connection(TcpClient tcpClient)
        {
            Connected = true;

            VCList = new Queue<VesselControls>();
            MCPList = new Queue<ManChangePacket>();
            sendQueue = new Queue<byte[]>();

            client = tcpClient;
            ns = client.GetStream();

            HeaderArray h_ = new HeaderArray();
            unsafe
            {
                for (int i = 0; i < Header_Array.Length; i++)
                {
                    h_.header[i] = Header_Array[i];
                }
            }

            sendSP = new SendSP
            {
                header = new Header
                {
                    header = h_,
                    length = (UInt16)Marshal.SizeOf(typeof(StatusPacket)),
                    type = 1
                }
            };

            sendVP = new SendVP
            {
                header = new Header
                {
                    header = h_,
                    length = (UInt16)Marshal.SizeOf(typeof(VesselPacket)),
                    type = 2
                }
            };

            sendODPHeader = new Header
            {
                header = h_,
                type = 3
            };

            new Thread(new ThreadStart(RunRecieve)).Start();
            new Thread(new ThreadStart(RunWrite)).Start();
        }

        private void RunWrite()
        {
            while (Connected)
            {
                try
                {
                    if (sendQueue.Count != 0)
                    {
                        byte[] arr = null;
                        arr = sendQueue.Dequeue();
                        if (arr != null)
                        {
                            ns.Write(arr, 0, arr.Length);
                        }
                    }
                }
                catch (IOException)
                {
                    Debug.Log("Error Sending");
                    Connected = false;
                }
            }
        }

        private byte[] ReadBytes(int bytesToRead)
        {
            byte[] recv = new byte[bytesToRead];
            int bytesRead = 0;
           // Debug.Log("want to read: " + bytesToRead);
            while (bytesRead < bytesToRead)
            {
                bytesRead += ns.Read(recv, bytesRead, bytesToRead - bytesRead);
            }
           // Debug.Log("read...");
            return recv;
        }

        private void RunRecieve()
        {
            IntPtr ptr = Marshal.AllocHGlobal(256);
            while (Connected)
            {
               // Debug.Log("recieving");
                try
                {
                    Marshal.Copy(ReadBytes(Marshal.SizeOf(typeof(Header))), 0, ptr, Marshal.SizeOf(typeof(Header)));
                    Header header = (Header)Marshal.PtrToStructure(ptr, typeof(Header));

                    if (header.type == 1)
                    {
                        Marshal.Copy(ReadBytes(Marshal.SizeOf(typeof(ControlPacket))), 0, ptr, Marshal.SizeOf(typeof(ControlPacket)));
                        ControlPacket cp = (ControlPacket)Marshal.PtrToStructure(ptr, typeof(ControlPacket));
                        if (cp.ID > lastCPID)
                        {
                            lastCPID = cp.ID;
                            VCList.Enqueue(CPToVC(cp));
                            axisControls = CPToAC(cp);
                        }
                    }
                    else if (header.type == 2)
                    {
                        Marshal.Copy(ReadBytes(Marshal.SizeOf(typeof(ManChangePacket))), 0, ptr, Marshal.SizeOf(typeof(ManChangePacket)));
                        ManChangePacket mcp = (ManChangePacket)Marshal.PtrToStructure(ptr, typeof(ManChangePacket));
                        MCPList.Enqueue(mcp);
                    }
                    else
                    {
                        Debug.Log("Incorrect header");
                    }
                }
                catch (IOException)
                {
                    Debug.Log("Error Recieving");
                    Connected = false;
                }
            }
            Marshal.FreeHGlobal(ptr);
        }

        public void SyncControls(VesselControls vc)
        {
            VCOld = vc;
        }

        public bool HaveVCPackets()
        {
            return VCList.Count != 0;
        }
        public bool HaveMCPPackets()
        {
            return MCPList.Count != 0;
        }

        public VCDifference GetVC()
        {
            VesselControls newVC_ = VCList.Dequeue();
            VCDifference vcDIff = new VCDifference()
            {
                oldVC = VCOld,
                newVC = newVC_
            };
            VCOld = newVC_;
            return vcDIff;
        }

        public ManChangePacket GetMCP()
        {
            return MCPList.Dequeue();
        }

        public void SendFlightPlanPacket(RawOrbitPlanData rawData)
        {
            if (!Connected) return;
            int numOrbits = rawData.CurrentOrbitPatches.Count;
            int numPlannedOrbits = rawData.PlannedOrbitPatches.Count;
            int numMans = rawData.Mans.Count;
            int targetNameLength = rawData.TargetName.Length;
            sendODPHeader.length = (UInt16)(6 + Marshal.SizeOf(typeof(OrbitData)) * (numOrbits + numPlannedOrbits + 1)
                + Marshal.SizeOf(typeof(ManData)) * numMans
                + targetNameLength + Marshal.SizeOf(typeof(ClosestAprouchData)));
            int fullPayloadLength = sendODPHeader.length + Marshal.SizeOf(typeof(Header));
            byte[] payload = new byte[fullPayloadLength]; //copy to here

            int copyOffset = Marshal.SizeOf(typeof(Header));
            payload[copyOffset++] = (byte)numOrbits; //1

            IntPtr ptr;
            ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(OrbitData)));
            for (int i = 0; i < numOrbits; i++)
            {
                Marshal.StructureToPtr(rawData.CurrentOrbitPatches[i], ptr, true);
                Marshal.Copy(ptr, payload, copyOffset, Marshal.SizeOf(typeof(OrbitData)));
                copyOffset += Marshal.SizeOf(typeof(OrbitData));
            }

            payload[copyOffset++] = (byte)rawData.ManPatchNum; //2
            payload[copyOffset++] = (byte)numPlannedOrbits; //3

            for (int i = 0; i < numPlannedOrbits; i++)
            {
                Marshal.StructureToPtr(rawData.PlannedOrbitPatches[i], ptr, true);
                Marshal.Copy(ptr, payload, copyOffset, Marshal.SizeOf(typeof(OrbitData)));
                copyOffset += Marshal.SizeOf(typeof(OrbitData));
            }

            //copy target orbit
            Marshal.StructureToPtr(rawData.TargetOrbit, ptr, true);
            Marshal.Copy(ptr, payload, copyOffset, Marshal.SizeOf(typeof(OrbitData)));
            copyOffset += Marshal.SizeOf(typeof(OrbitData));

            Marshal.FreeHGlobal(ptr);

            payload[copyOffset++] = (byte)targetNameLength; //copy target name //4
            for (int i = 0; i < targetNameLength; i++)
            {
                payload[copyOffset++] = (byte)rawData.TargetName[i];
            }
            payload[copyOffset++] = 0x00; //5

            ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ClosestAprouchData)));
            Marshal.StructureToPtr(rawData.Rendezvous, ptr, true);
            Marshal.Copy(ptr, payload, copyOffset, Marshal.SizeOf(typeof(ClosestAprouchData)));
            Marshal.FreeHGlobal(ptr);
            copyOffset += Marshal.SizeOf(typeof(ClosestAprouchData));

            payload[copyOffset++] = (byte)numMans; //menuever nodes  //6

            ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ManData)));
            for (int i = 0; i < numMans; i++)
            {
                Marshal.StructureToPtr(rawData.Mans[i], ptr, true);
                Marshal.Copy(ptr, payload, copyOffset, Marshal.SizeOf(typeof(ManData)));
                copyOffset += Marshal.SizeOf(typeof(ManData));
            }
            Marshal.FreeHGlobal(ptr);

            sendODPHeader.checksum = Checksum(payload, Marshal.SizeOf(typeof(Header)), sendODPHeader.length); //calc checksum

            ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Header))); //copy header to array
            Marshal.StructureToPtr(sendODPHeader, ptr, true);
            Marshal.Copy(ptr, payload, 0, Marshal.SizeOf(typeof(Header)));
            Marshal.FreeHGlobal(ptr);

            sendQueue.Enqueue(payload); //enqueue data
        }

        public void SendStatusPacket(StatusPacket sp)
        {
            if (!Connected) return;
            sp.ID = ++SPc;
            sendSP.sp = sp;

            UInt16 checksum;
            unsafe
            {
                checksum = Checksum((byte*)&sp, sizeof(StatusPacket));
            }

            sendSP.header.checksum = checksum;

            int size = Marshal.SizeOf(sendSP);
            byte[] arr = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(sendSP, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            sendQueue.Enqueue(arr);
        }

        public void SendVesselPacket(VesselPacket vp)
        {
            if (!Connected) return;
            vp.ID = ++VPc;
            sendVP.vp = vp;

            UInt16 checksum;
            unsafe
            {
                checksum = Checksum((byte*)&vp, sizeof(VesselPacket));
            }

            sendVP.header.checksum = checksum;

            int size = Marshal.SizeOf(sendVP);
            byte[] arr = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(sendVP, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            sendQueue.Enqueue(arr);
        }

        public void Send(byte[] arr) //make asynchronous
        {
        }


        /*[StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct SendOrbitDataPacket
        {
            public Header header;
            //public int ID;
            //public byte NumOrbits;
            //Future Orbits go here
        }*/

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct SendSP
        {
            public Header header;
            public StatusPacket sp;
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct SendVP
        {
            public Header header;
            public VesselPacket vp;
        };

        private UInt16 Checksum(byte[] buffer, int offset, int length)
        {
            UInt16 acc = 0;
            for (int i = offset; i < offset + length; i++)
            {
                acc += buffer[i];
            }
            return acc;
        }

        private unsafe UInt16 Checksum(byte* buffer, int length)
        {
            UInt16 acc = 0;
            for (int i = 0; i < length; i++)
            {
                acc += buffer[i];
            }
            return acc;
        }
    }
}
