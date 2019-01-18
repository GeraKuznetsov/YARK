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
        Queue<byte[]> sendQueue;
        NetworkStream ns;
        SendSP sendSP;
        SendVP sendVP;
        VesselControls VCOld;

        int SPc = 0, VPc = 0;

        int lastCPID = -1;

        bool Connected;

        public bool GetConnected()
        {
            return Connected;
        }

        public Connection(TcpClient tcpClient)
        {
            Connected = true;

            VCList = new Queue<VesselControls>();
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
                    type = 1,
                }
            };

            sendVP = new SendVP
            {
                header = new Header
                {
                    header = h_,
                    type = 2,
                }
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
                catch (IOException e)
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
            while (bytesRead < bytesToRead)
            {
                bytesRead = ns.Read(recv, bytesRead, bytesToRead - bytesRead);
            }
            return recv;
        }

        private void RunRecieve()
        {
            IntPtr ptr = Marshal.AllocHGlobal(256);
            while (Connected)
            {
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
                        }
                    }
                }
                catch (IOException e)
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

        public bool HavePackets()
        {
            return VCList.Count != 0;
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

        public void SendStatusPacket(StatusPacket sp)
        {
            if (!Connected) return;
            sp.ID = SPc;
            sendSP.sp = sp;

            UInt16 checksum;
            unsafe            {

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

            SPc++;
        }

        public void SendVesselPacket(VesselPacket vp)
        {
            if (!Connected) return;
            vp.ID = VPc;
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

            VPc++;
        }

        public void Send(byte[] arr) //make asynchronous
        {
        }

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
