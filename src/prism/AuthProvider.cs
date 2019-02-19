using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Prism
{
    public class AuthProvider
    {
        private string _credProviderPath = @"D:\code\VSO\tool\nuget\CredProviderPlugin\plugins\netfx\CredentialProvider.Microsoft\CredentialProvider.Microsoft.exe";

        private string BearerToken;
        private object lockKey = new object();

        public bool CanAuthenticate(Uri destination)
        {
            if (!destination.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase)
                && !destination.Host.EndsWith("dev.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        public AuthenticationHeaderValue GetAuthenticationHeader(Uri destination, bool retry)
        {
            if (!CanAuthenticate(destination))
            {
                throw new InvalidRequestException("Auth may only be provided to .visualstudio.com or dev.azure.com addresses");
            }

            string exceptionString = null;

            if (retry || string.IsNullOrEmpty(BearerToken))
            {
                lock (lockKey)
                {
                    if (retry || string.IsNullOrEmpty(BearerToken))
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
                                BearerToken = line.Substring(index + "Password:".Length).Trim();
                                break;
                            }
                        }

                        exceptionString = $"{proc.ExitCode} : {proc.StandardError.ReadToEnd()}";
                    }
                }

                if (string.IsNullOrEmpty(BearerToken))
                {
                    throw new AuthorizationException($"Couldn't validate to {destination} - auth failure: {exceptionString}");
                }
            }

            return new AuthenticationHeaderValue("Bearer", BearerToken);
        }
    }
}
