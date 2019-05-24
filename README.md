# prism
Local opt-in passthru service to unify diagnostics, telemetry, cache control, and more, all under a user's control.

## Outlook
Fiddler is my favorite full-featured system for true and full intercept control, but it can be hard to configure,
share, deploy anywhere it is needed, and explain. This more fit-for-purpose tool won't have the power of fiddler, but can be a strong
ubiquitous tool nonetheless.  As a dev in the Packaging Server space (NuGet, npm, python/PyPi, maven, etc), I'm constantly trying to work with
and around existing tooling, and this is my current exploration to avoid building deep plugins and plugin architectures into 
every packaging client.

## Scenarios
Let's focus on NuGet.  Most of what we have here easily translates across protocol, but rooting in specifics is easier.

nuget.exe is going to use one or more nuget.config files to identity multiple package sources.  Each package source has its own
authentication information which can vary from commandline-input to preconfigured (encrypted or not) to dynamic via credential providers.
In one execution it can be pulling many packages from a graph, some of which already exist locally and some have to be fetched.  It asks
all http(s) package sources in parallel for discovery information and takes the first responder. Often that followup download of a package
is going to end up following a redirect to Azure storage or S3 somewhere.

To avoid terminating-https MITM cert management and the scary dialogs that come with that, we'll opt-in via http://localhost urls that
are forced to https on the outgoing end.  I haven't quite managed how I'll facilitate just-in-time url replacement in client configs
to avoid every dev being required to run this tool...for future us.

#### Diagnostics
A page showing simple network traffic with an emphasis on common problems : auth, proxies, urls, headers
#### Telemetry
A page showing telemetry to send - you see and control what can be sent or if it is sent. Client-side telemetry is great for services,
and if you are having issues/problems it only helps to let some basic telemetry flow usptream
#### Blob download
Potential smarter blob download with resume or block parallelization. For Azure Devops, potentially act as a chunk cache
#### Look-aside caching
Have a trusted P2P or onprem content mirror faster than your network connection up to a CDN/Azure/S3?
#### Auth
This is a long shot to be secure. Most packaging client tools have been easy to configure against public repositories.  When it comes
to auth they all behave differently and with varying quality.  If the localhost loopback can ensure the identity of the caller without
local auth (port matching?) then I'd love to configure cred providers once in this tool than for every individual tool.
#### Logging Automation
In CI/CD you can live on logs and die on flaky tests/services.  Being able to run the service, route the traffic, and dump the session
improves the default client experiences.
