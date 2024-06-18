using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;

namespace NetNamedPipePathSearcher
{
    public class PathSearcher
    {
        public static List<SearchedEndpoint> SearchPath(Uri uri)
        {
            ILoggerFactory factory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
            ILogger logger = factory.CreateLogger<PathSearcher>();
            return SearchPath(uri, logger);
        }

        public static List<SearchedEndpoint> SearchPath(Uri uri, ILogger logger)
        {
            var searchedEndpoints = new List<SearchedEndpoint>();
            SearchedEndpoint? bestMatchedEndpoint = null, firstMatchedEndpoint = null;
            HostNameComparisonMode[] hostNameComparisons = new HostNameComparisonMode[] { HostNameComparisonMode.StrongWildcard, HostNameComparisonMode.Exact, HostNameComparisonMode.WeakWildcard };
            string[] hostChoices = new string[] { "+", uri.Host, "*" };
            bool[] globalChoices = new bool[] { true, false };
            string matchPath = String.Empty;
            string matchPipeName = String.Empty;

            foreach (var comparisonMode in hostNameComparisons)
            {
                string hostname = string.Empty;
                switch (comparisonMode)
                {
                    case HostNameComparisonMode.StrongWildcard:
                        hostname = "+";
                        break;
                    case HostNameComparisonMode.Exact:
                        hostname = uri.Host;
                        break;
                    case HostNameComparisonMode.WeakWildcard:
                        hostname = "*";
                        break;
                }

                foreach (var useGlobal in globalChoices)
                {
                    // walk up the path hierarchy, looking for match
                    string path = GetPath(uri);
                    while (path.Length > 0)
                    {
                        string sharedMemoryName = BuildSharedMemoryName(logger, hostname, path, useGlobal);
                        var searchedEndpoint = new SearchedEndpoint
                        {
                            SharedMemoryName = sharedMemoryName,
                            Path = path,
                            HostName = hostname,
                            HostNameComparisonMode = comparisonMode,
                            UseGlobal = useGlobal
                        };
                        logger.LogTrace("Checking shared memory with name {SharedMemoryName}", sharedMemoryName);
                        try
                        {
                            PipeSharedMemory? sharedMemory = PipeSharedMemory.Open(logger, sharedMemoryName, uri);
                            if (sharedMemory != null)
                            {
                                searchedEndpoint.SharedMemoryFound = true;
                                try
                                {
                                    string? pipeName = sharedMemory.GetPipeName();
                                    if (pipeName != null)
                                    {
                                        searchedEndpoint.PipeName = pipeName;
                                        if (firstMatchedEndpoint == null)
                                        {
                                            firstMatchedEndpoint = searchedEndpoint;
                                        }
                                        if (path.Length > matchPath.Length)
                                        {
                                            if (matchPath.Length == 0)
                                            {
                                                logger.LogTrace("Found service listening at path {Path}. Will keep searching.", path);
                                            }
                                            else
                                            {
                                                logger.LogTrace("Service listening at path {Path} is more specific than previously found service listening at path {MatchPath}", path, matchPath);
                                            }
                                            bestMatchedEndpoint = searchedEndpoint;
                                            matchPath = path;
                                            matchPipeName = pipeName;
                                        }
                                    }
                                }
                                finally
                                {
                                    sharedMemory.Dispose();
                                }
                            }
                            else
                            {
                                searchedEndpoint.SharedMemoryFound = false;
                            }
                        }
                        catch (AddressAccessDeniedException exception)
                        {
                            throw new EndpointNotFoundException($"EndpointNotFound, {uri.AbsoluteUri}", exception);
                        }

                        searchedEndpoints.Add(searchedEndpoint);
                        path = GetParentPath(path);
                    }
                }
            }

            if (bestMatchedEndpoint != null)
            {
                bestMatchedEndpoint.IsBestMatch = true;
            }

            if (firstMatchedEndpoint != null)
            {
                firstMatchedEndpoint.IsFirstMatch = true;
            }

            return searchedEndpoints;
        }

        private static string GetPath(Uri uri)
        {
            string path = uri.LocalPath.ToUpperInvariant();
            if (!path.EndsWith("/", StringComparison.Ordinal))
                path = path + "/";
            return path;
        }

        private static string GetParentPath(string path)
        {
            if (path.EndsWith("/", StringComparison.Ordinal))
                path = path.Substring(0, path.Length - 1);
            if (path.Length == 0)
                return path;
            return path.Substring(0, path.LastIndexOf('/') + 1);
        }

        private static string BuildSharedMemoryName(ILogger logger, string hostName, string path, bool global)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Uri.UriSchemeNetPipe);
            builder.Append("://");
            builder.Append(hostName.ToUpperInvariant());
            builder.Append(path);
            string canonicalName = builder.ToString();
            logger.LogTrace("Using canonical name {CanonicalName} for hostname {HostName} and path {Path}", canonicalName, hostName, path);
            byte[] canonicalBytes = Encoding.UTF8.GetBytes(canonicalName);
            byte[] hashedBytes;
            string separator;

            if (canonicalBytes.Length >= 128)
            {
                using (HashAlgorithm hash = GetHashAlgorithm())
                {
                    logger.LogTrace("Canonical name too long so hashing using {Algorithm}", hash.GetType().Name);
                    hashedBytes = hash.ComputeHash(canonicalBytes);
                }
                separator = ":H";
            }
            else
            {
                hashedBytes = canonicalBytes;
                separator = ":E";
            }

            builder = new StringBuilder();
            if (global)
            {
                // we may need to create the shared memory in the global namespace so we work with terminal services+admin
                builder.Append("Global\\");
            }
            else
            {
                builder.Append("Local\\");
            }
            builder.Append(Uri.UriSchemeNetPipe);
            builder.Append(separator);
            builder.Append(Convert.ToBase64String(hashedBytes));
            return builder.ToString();
        }

        private static Func<HashAlgorithm>? s_getHashAlgorithmFunc = null;
        private static HashAlgorithm GetHashAlgorithm()
        {
            if (s_getHashAlgorithmFunc == null)
            {
                var pipeUriType = typeof(NetNamedPipeBinding).Assembly.GetType("System.ServiceModel.Channels.PipeUri");
                var getHashAlgoMethodInfo = pipeUriType.GetMethod("GetHashAlgorithm", BindingFlags.Static | BindingFlags.NonPublic);
                s_getHashAlgorithmFunc = (getHashAlgoMethodInfo.CreateDelegate(typeof(Func<HashAlgorithm>)) as Func<HashAlgorithm>)!;
            }

            return s_getHashAlgorithmFunc();
        }

        private static Func<bool>? s_useBestMatchNamedPipeUri = null;
        private static bool UseBestMatchNamedPipeUri()
        {
            if (s_useBestMatchNamedPipeUri == null)
            {
                var appSettingsType = typeof(NetNamedPipeBinding).Assembly.GetType("System.ServiceModel.ServiceModelAppSettings");
                var bestMatchNamedPropertyInfo = appSettingsType.GetProperty("UseBestMatchNamedPipeUri", BindingFlags.Static | BindingFlags.NonPublic);
                var bestMatchNamedPropertyGetterMethodInfo = bestMatchNamedPropertyInfo.GetGetMethod(true);
                s_useBestMatchNamedPipeUri = (bestMatchNamedPropertyGetterMethodInfo.CreateDelegate(typeof(Func<bool>)) as Func<bool>)!;
            }

            return s_useBestMatchNamedPipeUri();
        }
    }
}