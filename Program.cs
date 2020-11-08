using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Policy;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConnectedDevices
{
    //switch between backround and forground application: Project > Properties > Application > Output type "Console application"(forground) or "Windows application"(background).
    class Program
    {
        static List<string> freshMacs = new List<string>();
        static List<Device> devices = new List<Device>();

        static int refreshRate = 60000;//in millisec

        static bool booting = true;

        static void Main(string[] args)
        {
            while (true)
            {
                freshMacs.Clear();
                Ping_all();

                Thread.Sleep(refreshRate);

                CheckDeviceConnectivities();
                PrintDevices();

                Console.WriteLine();
                Console.WriteLine();
            }
        }

        static void PrintDevices()
        {
            Console.WriteLine(DateTime.Now);
            Console.WriteLine("devices: {0}, online: {1}, offline: {2}", devices.Count, devices.Where(x => x.connected).ToList().Count, devices.Where(x => !x.connected).ToList().Count);

            foreach (Device dev in devices.OrderByDescending(x => x.connected).ThenBy(x => x.ip))
            {
                Console.WriteLine("connected: {0}, ip: {1}, mac: {2}, name: {3}", dev.connected ? "1" : "0", dev.ip, dev.mac, !string.IsNullOrEmpty(dev.name) ? dev.name : " - ");
            }
        }

        static void CheckDeviceConnectivities()
        {
            SpeechSynthesizer synthesizer = new SpeechSynthesizer();
            synthesizer.SetOutputToDefaultAudioDevice();

            foreach (Device dev in devices)
            {
                if (freshMacs.Contains(dev.mac) != dev.connected)
                {
                    if (!dev.connected)//connected
                    {
                        if (!booting)
                        {
                            synthesizer.Speak(string.Format("{0} has been connected.", !string.IsNullOrEmpty(dev.name) ? dev.name : "A new device"));
                        }
                        dev.connected = true;
                    }
                    else//disconnected
                    {
                        if (!booting)
                        {
                            synthesizer.Speak(string.Format("{0} has been disconnected.", !string.IsNullOrEmpty(dev.name) ? dev.name : "A device"));
                        }
                        dev.connected = false;
                    }
                }
            }

            if (booting)
            {
                booting = false;
            }
        }

        static string NetworkGateway()
        {
            string ip = null;

            foreach (NetworkInterface f in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (f.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (GatewayIPAddressInformation d in f.GetIPProperties().GatewayAddresses)
                    {
                        ip = d.Address.ToString();
                    }
                }
            }

            return ip;
        }

        public static void Ping_all()
        {

            string gate_ip = NetworkGateway();

            //Extracting and pinging all other ip's.
            string[] array = gate_ip.Split('.');

            for (int i = 2; i <= 255; i++)
            {

                string ping_var = array[0] + "." + array[1] + "." + array[2] + "." + i;

                //time in milliseconds           
                Ping(ping_var, 4, 10000);

            }

        }

        public static void Ping(string host, int attempts, int timeout)
        {
            for (int i = 0; i < attempts; i++)
            {
                new Thread(delegate ()
                {
                    try
                    {
                        System.Net.NetworkInformation.Ping ping = new System.Net.NetworkInformation.Ping();
                        ping.PingCompleted += new PingCompletedEventHandler(PingCompleted);
                        ping.SendAsync(host, 10000, host);
                    }
                    catch
                    {
                        // Do nothing and let it try again until the attempts are exausted.
                        // Exceptions are thrown for normal ping failurs like address lookup
                        // failed.  For this reason we are supressing errors.
                    }
                }).Start();
            }
        }

        static void PingCompleted(object sender, PingCompletedEventArgs e)
        {
            string ip = (string)e.UserState;
            if (e.Reply != null && e.Reply.Status == IPStatus.Success)
            {
                string hostname = GetHostName(ip);
                string macaddres = GetMacAddress(ip);

                if (!freshMacs.Contains(macaddres))
                {
                    freshMacs.Add(macaddres);
                }

                if (devices.Find(x => x.mac == macaddres) == null)
                {
                    devices.Add(new Device(hostname, ip, macaddres, false));
                }
            }
            else
            {
                // MessageBox.Show(e.Reply.Status.ToString());
            }
        }

        public static string GetHostName(string ipAddress)
        {
            try
            {
                IPHostEntry entry = Dns.GetHostEntry(ipAddress);
                if (entry != null)
                {
                    return entry.HostName;
                }
            }
            catch (SocketException)
            {
                //MessageBox.Show(e.Message.ToString());
            }

            return null;
        }


        //Get MAC address
        public static string GetMacAddress(string ipAddress)
        {
            string macAddress = string.Empty;
            System.Diagnostics.Process Process = new System.Diagnostics.Process();
            Process.StartInfo.FileName = "arp";
            Process.StartInfo.Arguments = "-a " + ipAddress;
            Process.StartInfo.UseShellExecute = false;
            Process.StartInfo.RedirectStandardOutput = true;
            Process.StartInfo.CreateNoWindow = true;
            Process.Start();
            string strOutput = Process.StandardOutput.ReadToEnd();
            string[] substrings = strOutput.Split('-');
            if (substrings.Length >= 8)
            {
                macAddress = substrings[3].Substring(Math.Max(0, substrings[3].Length - 2))
                         + "-" + substrings[4] + "-" + substrings[5] + "-" + substrings[6]
                         + "-" + substrings[7] + "-"
                         + substrings[8].Substring(0, 2);
                return macAddress;
            }
            else
            {
                return "OWN Machine";
            }
        }
    }

    public class Device
    {
        public string name;
        public string ip;
        public string mac;

        public bool connected;

        public Device(string name, string ip, string mac, bool connected)
        {
            this.name = name;
            this.ip = ip;
            this.mac = mac;
            this.connected = connected;
        }
    }
}