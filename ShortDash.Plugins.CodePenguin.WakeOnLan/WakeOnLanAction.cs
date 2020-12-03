using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using ShortDash.Core.Plugins;

namespace ShortDash.Plugins.CodePenguin.WakeOnLan
{
    [ShortDashAction(
        Title = "Wake on LAN",
        Description = "Sends a Wake On LAN message to a machine on the local network.",
        ParametersType = typeof(WakeOnLanParameters))]
    public class WakeOnLanAction : IShortDashAction
    {
        public ShortDashActionResult Execute(object parametersObject, bool toggleState)
        {
            var parameters = parametersObject as WakeOnLanParameters;
            SendWakeOnLan(parameters.MacAddress);
            return new ShortDashActionResult { Success = true, UserMessage = "Wake on LAN message sent." };
        }

        private static void SendWakeOnLan(string macAddress)
        {
            var packet = WakeOnLanPacket(macAddress);
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback && n.OperationalStatus == OperationalStatus.Up);
            foreach (var networkInterface in networkInterfaces)
            {
                var ipProperties = networkInterface.GetIPProperties();
                foreach (var multicastAddress in ipProperties.MulticastAddresses)
                {
                    var address = multicastAddress.Address.ToString();
                    var isIpV4 = address.Equals("224.0.0.1");
                    var isIpV6 = address.StartsWith("ff02::1%", StringComparison.OrdinalIgnoreCase);
                    if (!isIpV4 && !isIpV6) continue;
                    var unicastAddress = ipProperties.UnicastAddresses.Where(u =>
                        (isIpV4 && u.Address.AddressFamily == AddressFamily.InterNetwork && !ipProperties.GetIPv4Properties().IsAutomaticPrivateAddressingActive) ||
                        (isIpV6 && u.Address.AddressFamily == AddressFamily.InterNetworkV6 && !u.Address.IsIPv6LinkLocal)).FirstOrDefault();
                    if (unicastAddress == null) continue;
                    SendWakeOnLan(unicastAddress.Address, multicastAddress.Address, packet);
                    break;
                }
            }
        }

        private static void SendWakeOnLan(IPAddress localIpAddress, IPAddress multicastIpAddress, byte[] packet)
        {
            using UdpClient client = new UdpClient(new IPEndPoint(localIpAddress, 0));
            client.Send(packet, packet.Length, multicastIpAddress.ToString(), 9);
        }

        private static byte[] WakeOnLanPacket(string macAddress)
        {
            macAddress = Regex.Replace(macAddress, "[-:]", string.Empty);
            var hexString = string.Concat(Enumerable.Repeat("FF", 6)) + string.Concat(Enumerable.Repeat(macAddress, 16));
            var packet = new byte[hexString.Length / 2];
            for (int i = 0; i < hexString.Length; i += 2)
            {
                packet[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            }
            return packet;
        }
    }

    public class WakeOnLanParameters
    {
        [Display(Name = "MAC Address")]
        [RegularExpression("(?:[0-9A-Fa-f]{2}[-:]{0,1}){6}", ErrorMessage="Invalid MAC Address.")]
        [Required]
        public string MacAddress { get; set; } = "";
    }
}