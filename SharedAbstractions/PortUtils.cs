using LoadBalancer.Exceptions;
using LoadBalancer.Logger;
using System.Net;
using System.Net.Sockets;

namespace LoadBalancer
{
    public static class PortUtils
    {
        private static readonly object _portLock = new object();
        private static readonly HashSet<int> _allocatedPorts = new();

        public static int FindAvailablePort(int startPort = 5001, int endPort = 65535)
        {
            lock (_portLock)
            {
                for (int port = startPort; port <= endPort; port++)
                {
                    if (_allocatedPorts.Contains(port))
                    {
                        Log.Debug($"Port {port} is already taken by LoadBalancer, searching for a new one...");
                        continue;
                    }

                    if (IsPortAvailable(port))
                    {
                        _allocatedPorts.Add(port);
                        Log.Debug($"Port {port} is now taken by LoadBalancer");
                        return port;
                    }
                }

                throw new InvalidOperationException($"No available ports were found in the specified range of {startPort} to {endPort}");
            }
        }

        public static void ReleasePort(int port)
        {
            lock (_portLock)
            {
                _allocatedPorts.Remove(port);
            }
        }

        private static bool IsPortAvailable(int port)
        {
            try
            {
                using var tcpListener = new TcpListener(IPAddress.Loopback, port);
                tcpListener.Start();
                tcpListener.Stop();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            catch (Exception ex) when (ex is LoadBalancerException)
            {
                Log.Error($"Error checking port {port} availability", ex);
                return false;
            }
        }
    }
}