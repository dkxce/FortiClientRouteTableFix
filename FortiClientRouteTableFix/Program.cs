//
// C# (.Net Framework) Windows Win7+
// FortiClient Route IPTable Fixer
// v 0.1, 14.07.2025
// https://github.com/dkxce
// en,ru,1251,utf-8
//

using System;
using System.Collections.Generic;
using System.Management;
using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;

namespace FortiClientRouteTableFix
{
    internal class Program
    {
        static void Main(string[] args) => IPv4RouteChecker.StartChecking();
    }

    internal static class IPv4RouteChecker
    {
        private const string  defMetric    = "100";
        private const string  vpnName      = "forti";
        private const int     interval     = 90;
        private static string defGateway   = "";
        private static string defIndex     = "";
        private static string defInterface = "";
        private static bool   preventNextSleep = false;
        private static List<string> ipList = new List<string>();       

        static IPv4RouteChecker()
        {
            Dictionary<string,string> descs = new Dictionary<string,string>(); ;
            string ip = "0.0.0.0";

            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_NetworkAdapter");
                foreach (ManagementObject queryObj in searcher.Get())
                    descs.Add(queryObj["Index"].ToString(), queryObj["Description"].ToString());
                searcher.Dispose();
            }
            catch (Exception ex) 
            {
                Console.WriteLine(ex);
                Conso1e.ReadLine(5000);
            };

            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_IP4RouteTable");
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    try
                    {
                        string Caption = queryObj["Caption"].ToString();
                        if (Caption != ip) continue;
                        string Protocol = queryObj["Protocol"].ToString();
                        //if (Protocol != "3") continue;

                        string InterfaceIndex = queryObj["InterfaceIndex"].ToString();
                        string Description = descs[InterfaceIndex];
                        if (Description.ToLower().Contains("vpn") || Description.ToLower().Contains("adapter") || Description.ToLower().Contains(vpnName))
                            continue;

                        defGateway = queryObj["NextHop"].ToString();
                        defIndex = InterfaceIndex;
                        defInterface = Description;
                        Console.WriteLine($"Direct Adapter: {Caption} -> {defGateway} `{defIndex} {defInterface}`");
                    }
                    catch { };
                };
                searcher.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Conso1e.ReadLine(5000);
            };

            try
            {
                string fName = System.Reflection.Assembly.GetEntryAssembly().Location;
                fName = Path.Combine(Path.GetDirectoryName(fName), "IPLIST.txt");
                ipList.AddRange(File.ReadAllLines(fName));
                for (int i = ipList.Count - 1; i >= 0; i--)
                {
                    if (ipList[i].Contains(":"))
                        ipList.RemoveAt(i);
                    else
                        ipList[i] = ipList[i].Trim().Split('/')[0];
                };

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Conso1e.ReadLine(5000);
            };

            Conso1e.ReadLine(1000);
        }

        public static void StartChecking()
        {
            // CHECK LIST
            if (ipList.Count == 0)
            {
                Console.WriteLine("NO IP LIST, past IP addresses into IPLIST.txt: 1 record per 1 line");
                Conso1e.ReadLine(5000);
                return;
            };
            Console.WriteLine($"Loaded {ipList.Count} IPs:");
            foreach (string ip in ipList) Console.WriteLine($" - {ip}");
            Conso1e.ReadLine(1000);

            // LOOP
            while (true)
            {
                LoopChecking(ipList);
                //PrintIP();


                if (preventNextSleep)
                {
                    preventNextSleep = false;
                    Console.WriteLine($"{DateTime.Now}: Wait {5} sec till {DateTime.Now.AddSeconds(5)}...");
                    Thread.Sleep(5 * 1000);
                }
                else
                {
                    Console.WriteLine($"{DateTime.Now}: Wait {interval} sec till {DateTime.Now.AddSeconds(interval)}...");
                    Thread.Sleep(interval * 1000);
                };
            };
        }

        private static void LoopChecking(List<string> ipList)
        {
            Console.WriteLine("");
            Console.WriteLine($"{DateTime.Now}: ------------------------------------------------------ ");
            Console.WriteLine($"{DateTime.Now}: Start Checking");
       
            foreach (string ip in ipList)
            {
                Console.WriteLine($"{DateTime.Now}:   Checking ip {ip}");
                try
                {
                    IPEndPoint gatewayIp = QueryRoutingGateway(new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp), new IPEndPoint(IPAddress.Parse(ip), 0));
                    string gatewayId = QueryRoutingGatewayId(gatewayIp.Address.ToString());
                    string gatewayName = QueryRoutingGatewayName(gatewayId);

                    bool ok = !gatewayName.ToLower().Contains(vpnName);
                    string txt = ok ? "OK" : vpnName;                    
                    Console.WriteLine($"{DateTime.Now}:     Route: {gatewayIp.Address} by {gatewayName} - {txt}");
                    if (!ok)
                        PassIP(ip);
                    else
                        NeedIP(ip);
                }
                catch (Exception ex)
                {
                    preventNextSleep = true;
                    Console.WriteLine($"{DateTime.Now}:     Error: {ex}");
                };
            };

            Console.WriteLine($"{DateTime.Now}: Checked");
        }

        private static IPEndPoint QueryRoutingGateway(Socket socket, IPEndPoint remoteEndPoint)
        {
            SocketAddress address = remoteEndPoint.Serialize();

            byte[] remoteAddrBytes = new byte[address.Size];
            for (int i = 0; i < address.Size; i++) remoteAddrBytes[i] = address[i];

            byte[] outBytes = new byte[remoteAddrBytes.Length];
            socket.IOControl(IOControlCode.RoutingInterfaceQuery, remoteAddrBytes, outBytes);
            for (int i = 0; i < address.Size; i++) address[i] = outBytes[i];

            EndPoint ep = remoteEndPoint.Create(address);
            return (IPEndPoint)ep;
        }

        private static string QueryRoutingGatewayId(string gatewayIp)
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                foreach (UnicastIPAddressInformation ipa in ni.GetIPProperties().UnicastAddresses)
                    if (ipa.Address.AddressFamily == AddressFamily.InterNetwork)
                        if (ipa.Address.ToString() == gatewayIp)
                            return ni.Name;
            return string.Empty;
        }

        private static string QueryRoutingGatewayName(string gatewayId)
        {
            ManagementScope oMs = new ManagementScope();
            ObjectQuery oQuery = new ObjectQuery($"Select * From Win32_NetworkAdapter WHERE NetConnectionID = \"{gatewayId}\"");
            ManagementObjectSearcher oSearcher = new ManagementObjectSearcher(oMs, oQuery);
            ManagementObjectCollection oReturnCollection = oSearcher.Get();
            foreach (ManagementObject oReturn in oReturnCollection)
                if (oReturn.Properties["ProductName"].Value != null)
                    return oReturn.Properties["ProductName"].Value.ToString();
            return string.Empty;
        }

        private static void PassIP(string ip)
        {
            Console.WriteLine($"{DateTime.Now}:       Checking Existing Route for {ip} ...");
            
            string cmd = $"print {ip}";
            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo("route", cmd);
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.UseShellExecute = false;

            System.Diagnostics.Process p = System.Diagnostics.Process.Start(info);
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);

            bool ex = false;
            int vpnMetric = 0;
            int dirMetric = 0;
            string vpnGate = "";
            string vpnMask = "255.255.255.255";

            Regex rx = new Regex("[\\d\\.]+");
            foreach (string d in output.Split(new char[] { '\r', '\n' }))
                if (d.Trim().StartsWith(ip))
                {
                    MatchCollection mx = rx.Matches(d);
                    if (mx[0].Value == ip)
                    {
                        if (mx[2].Value == defGateway)
                        {
                            ex = true;
                            dirMetric = int.Parse(mx[4].Value);
                        }
                        else
                        {
                            vpnMetric = int.Parse(mx[4].Value);
                            vpnMask = mx[1].Value;
                            vpnGate = mx[2].Value;
                        };
                    };
                };
                                   
            if(ex)
            {
                if (vpnMetric < dirMetric)
                {
                    preventNextSleep = true;
                    Console.WriteLine($"{DateTime.Now}:         Metric found, rotating to Direct Gate: Direct={dirMetric}, {vpnName}={vpnMetric}");
                    if (string.IsNullOrEmpty(vpnGate))
                    {
                        Console.WriteLine($"{DateTime.Now}:           Error: Gateway is bad `{vpnGate}`");
                    }
                    else
                    {
                        string res = SetRouteMetric(ip, vpnMask, vpnGate, "1000");
                        Console.WriteLine($"{DateTime.Now}:           Status: {res}");
                    };
                }
                else
                {
                    Console.WriteLine($"{DateTime.Now}:         Metric found, no need rotate: Direct={dirMetric}, {vpnName}={vpnMetric}\r\n");
                };
            }
            else
            {
                preventNextSleep = true;
                Console.WriteLine($"{DateTime.Now}:         Metric NOT found, adding route to Direct Gate");
                string[] mm = ip.Split(new char[] { '.' });
                for (int i = 0; i < mm.Length; i++) if (mm[i] != "0") mm[i] = "255"; else mm[i] = "0";
                string mask = string.Join(".", mm);
                string txt = AddStaticRoute(ip, mask);
                Console.WriteLine($"{DateTime.Now}:         {txt}");                
            };
        }

        private static string AddStaticRoute(string destination, string mask)
        {
            string cmd = $"add {destination} mask {mask} {defGateway} metric {defMetric}";
            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo("route", cmd);
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.UseShellExecute = false;

            System.Diagnostics.Process p = System.Diagnostics.Process.Start(info);
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();
            return output + error;
        }

        private static string RemStaticRoute(string destination)
        {
            string cmd = $"delete {destination}";
            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo("route", cmd);
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.UseShellExecute = false;

            System.Diagnostics.Process p = System.Diagnostics.Process.Start(info);
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error)) preventNextSleep = true;
            return output + error;
        }

        private static string SetRouteMetric(string ip, string mask, string gate, string metric)
        {
            string cmd = $"change {ip} mask {mask} {gate} metric {metric}";
            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo("route", cmd);
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.UseShellExecute = false;

            System.Diagnostics.Process p = System.Diagnostics.Process.Start(info);
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);
            return output;
        }

        private static void NeedIP(string ip)
        {
            Console.WriteLine($"{DateTime.Now}:       Checking Existing Route for {ip} ...");

            string cmd = $"print {ip}";
            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo("route", cmd);
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.UseShellExecute = false;

            System.Diagnostics.Process p = System.Diagnostics.Process.Start(info);
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);

            int count = 0;
            bool ex = false;

            Regex rx = new Regex("[\\d\\.]+");
            foreach (string d in output.Split(new char[] { '\r', '\n' }))
                if (d.Trim().StartsWith(ip))
                {
                    count++;
                    MatchCollection mx = rx.Matches(d);
                    if (mx[0].Value == ip) ex = true;
                };
            if (count == 0)
                Console.WriteLine($"{DateTime.Now}:       No any routes for {ip}\r\n");
            else if (count == 1 && ex)
            {
                Console.WriteLine($"{DateTime.Now}:       Found not need Route for {ip}, deleting ...");
                string res = RemStaticRoute(ip);
                Console.WriteLine($"{DateTime.Now}:           Status: {res}\r\n");
            }
            else Console.WriteLine($"{DateTime.Now}:           No need changes to IP Route Table\r\n");
        }

        private static void PrintIP()
        {
            string cmd = $"print";
            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo("route", cmd);
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.UseShellExecute = false;

            System.Diagnostics.Process p = System.Diagnostics.Process.Start(info);
            p.Start();
            string output = p.StandardOutput.ReadToEnd();

            Console.WriteLine("-------------------------- IP TABLE --------------------------");
            Regex rx = new Regex("[\\d\\.]+");
            foreach (string d in output.Split(new char[] { '\r', '\n' }))
            {
                MatchCollection mx = rx.Matches(d);
                if (d.Contains(".") && mx.Count == 4 && (!d.Contains(":")))
                    Console.WriteLine(d.Trim());
            };
            Console.WriteLine("--------------------------------------------------------------");
        }
    }
}
