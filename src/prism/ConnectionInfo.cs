using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Prism
{
    public class ConnectionInfoFactory
    {
        private readonly IHttpContextAccessor _contextAccessor;
        private ClientConnectionInfo _connection;

        public ConnectionInfoFactory(IHttpContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
        }

        public async Task<ClientConnectionInfo> GetConnection()
        {
            if (_connection == null)
            {
                _connection = await ClientConnectionInfo.Resolve(
                    _contextAccessor.HttpContext.Connection.RemotePort,
                    _contextAccessor.HttpContext.Connection.LocalPort);
            }

            return _connection;
        }
    }

    public static class ConnectionInfoFactoryExtension
    {
        public static void AddConnectionInfoFactory(this IServiceCollection services)
        {
            services.AddScoped<ConnectionInfoFactory>();
        }
    }
    
    public class ClientConnectionInfo
    {
        private readonly static WindowsIdentity currentUser;

        private readonly WindowsIdentity localUser;
        private readonly WindowsIdentity remoteUser;
        private readonly int remotePortNum;
        private readonly int localPortNum;

        public readonly string Session;

        static ClientConnectionInfo()
        {
            currentUser = GetProcessUser(Process.GetCurrentProcess());
            if (currentUser == null)
            {
                throw new ApplicationException("Can't get the identity running the current process.");
            }
        }

        public bool IsRemoteUserSameAsCurrent => 
            !string.IsNullOrEmpty(localUser?.User?.Value) && string.Equals(localUser.User.Value, remoteUser?.User?.Value);
        
        public ClientConnectionInfo(WindowsIdentity localUser, int localPortNum, WindowsIdentity remoteUser, int remotePortNum, string session)
        {
            this.localUser = localUser;
            this.localPortNum = localPortNum;
            this.remoteUser = remoteUser;
            this.remotePortNum = remotePortNum;

            if (session != null)
            {
                session = Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(session)));
            }

            Session = session;
        }

        public static async Task<ClientConnectionInfo> Resolve(int remotePortNum, int localPortNum)
        {
            string session = null;
            
            var startInfo = new ProcessStartInfo("netstat.exe", "-a -n -o -p TCP")
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(startInfo);
            
            WindowsIdentity remoteUser = null;
            string line;
            while ((line = await proc.StandardOutput.ReadLineAsync()) != null)
            {
                //TCP    0.0.0.0:24             0.0.0.0:0              LISTENING       5836
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 5)
                {
                    continue;
                }
                var addr = parts[1].Split(':');
                if (addr.Length != 2 || !int.TryParse(addr[1], out int openRemotePort) || openRemotePort != remotePortNum)
                {
                    continue;
                }

                try
                {
                    var clientProc = Process.GetProcessById(int.Parse(parts[4]));
                    remoteUser = GetProcessUser(clientProc);
                    session = $"{clientProc.StartTime.Ticks}|{clientProc.Id}";
                }
                catch
                {
                }

                break;
            }
            
            proc.Close();

            return new ClientConnectionInfo(currentUser, localPortNum, remoteUser, remotePortNum, session);
        }



        private static WindowsIdentity GetProcessUser(Process process)
        {
            IntPtr processHandle = IntPtr.Zero;
            try
            {
                OpenProcessToken(process.Handle, 8, out processHandle);
                return new WindowsIdentity(processHandle);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (processHandle != IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                }
            }
        }


        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}
