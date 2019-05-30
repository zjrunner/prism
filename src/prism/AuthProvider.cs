using System;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace Prism
{
    public class AuthProvider
    {
        private string _credProviderPath = @"D:\code\VSO\tool\nuget\CredProviderPlugin\plugins\netfx\CredentialProvider.Microsoft\CredentialProvider.Microsoft.exe";

        private string _bearerToken;
        private readonly object _lockKey = new object();


        public bool CanAuthenticate(Uri destination, ClientConnectionInfo connection)
        {
            if (!connection.IsRemoteUserSameAsCurrent)
            {
                return false;
            }

            if (!destination.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase)
                && !destination.Host.EndsWith("dev.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }


            return true;
        }

        public AuthenticationHeaderValue GetAuthenticationHeader(Uri destination, ClientConnectionInfo connection, bool retry)
        {
            if (!CanAuthenticate(destination, connection))
            {
                throw new InvalidOperationException("Authentication is not allowed for this connection.");
            }

            string exceptionString = null;

            if (retry || string.IsNullOrEmpty(_bearerToken))
            {
                lock (_lockKey)
                {
                    if (retry || string.IsNullOrEmpty(_bearerToken))
                    {
                        Environment.SetEnvironmentVariable("NUGET_CREDENTIALPROVIDER_VSTS_TOKENTYPE", "SelfDescribing");
                        Environment.SetEnvironmentVariable("NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED", "true");

                        string retryArg = retry ? "-I" : null;
                        var procStart = new ProcessStartInfo(_credProviderPath, $"-C -V Information {retryArg} -U {destination}")
                        {
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        var proc = Process.Start(procStart);
                        proc.WaitForExit();

                        string line;
                        while ((line = proc.StandardOutput.ReadLine()) != null)
                        {
                            int index;
                            if ((index = line.IndexOf("Password:")) >= 0)
                            {
                                _bearerToken = line.Substring(index + "Password:".Length).Trim();
                                break;
                            }
                        }

                        exceptionString = $"{proc.ExitCode} : {proc.StandardError.ReadToEnd()}";
                    }
                }

                if (string.IsNullOrEmpty(_bearerToken))
                {
                    throw new AuthorizationException($"Couldn't validate to {destination} - auth failure: {exceptionString}");
                }
            }

            return new AuthenticationHeaderValue("Bearer", _bearerToken);
        }
    }
}
