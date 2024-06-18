# NetNamedPipePathSearcher

Sometimes when running multiple WCF services using named pipes, one service can be listening on a path which interferes with the other and cause clients to connect to the wrong service. Windows has 2 namespaces that a service can create kernel object in, the GLOBAL namespace, and the LOCAL namespace. When using terminal server sessions, each session will have its own LOCAL namespace, so if you want a client and server to be able to communicate with each other across different sessions, e.g. an installed system service and a client GUI app, the service needs to use the GLOBAL namespace. To use the GLOBAL namespace, a process must have the [SeCreateGlobalPrivilege](https://learn.microsoft.com/en-us/windows/win32/secauthz/privilege-constants) privilege. By default, a regular user account doesn't have this privilege. If running as Admin, then this privilege is available. A WCF service will attempt to create it's kernel objects in the GLOBAL namespace first, and if that fails, will create it in the LOCAL namespace. When a client is trying to connect to a NetNamedPipe service, it will look in the GLOBAL namespace first, and if not found, will look in the LOCAL namespace.  

### Shared Memory vs NamedPipe
When a WCF service opens a named pipe to accept clients, it initially passes an option to demand that no other proccesses are using that named pipe path. This is to prevent running two instances of a service on the same path. A side effect of doing this is that any lingering clients that still have a handle to a named pipe for a previous instance of a service would prevent restarting a service until all previous clients have properly closed their connections. To avoid this problem, a shared memory object is created based on the service hosts base address Uri. The actual named pipe is created using a uniquely generated random GUID. This GUID is then stored in the shared memory object so that new clients wishing to connect can read the GUID from the shared memory, and then connect to the service named pipe. When restarting a service, the new service instance will use a new unique GUID, thus avoiding the problem of lingering clients preventing a new service instance from listening on the named pipe.  

### Shared memory object name and client search algorithm
As stated, it's the shared memory object which is based on the listen Uri. The name of the shared object is derived from the Uri, it's not used directly. When configuring a service binding, you can specify a [HostNameComparisonMode](https://learn.microsoft.com/en-us/dotnet/api/system.servicemodel.channels.connectionorientedtransportbindingelement.hostnamecomparisonmode?view=netframework-4.6.2). The default is `StrongWildcard` which basically means we don't care what the hostname is, the fact the message got to the right machine is all we care about (e.g. if connecting using an IP address with HTTP/NetTcp). Other options are `Exact` (client and server must agree on hostname), or `WeakWildcard` (looks for the exact hostname to match first, and if not found, is okay ignoring the hostname). When deriving the shared memory name, this translate to the hostnames `"+"` for `StrongWildcard`, the specified hostname in the Uri (usually `"localhost"`) for `Exact`, and `"*"` for `WeakWildCard`. When looking for the service, all possible `StrongWildcard` paths are checked first, then `Exact`, and finally `WeakWildcard`.  
The next component that is permuted is GLOBAL vs LOCAL. This is as simple as prefixing the object name with either `GLOBAL\` or `LOCAL\`. All Uri path permutations are checked in the GLOBAL namespace first, then the LOCAL namespace. By default, this means any matching path in the GLOBAL namespace will be found first.  
The final compoent which is permuted is the path component of the service host base address. There's an important subtlety with the base address, if the base address path doesn't end with a `/`, the final path segment is treated as a filename and not part of the path, so is ignored. E.g. if you specify a base address path of `net.pipe://localhost/foo/bar/baz`, the final segment `baz` is ignored as it's not considered part of the path. In this example, the path is considered to be `/foo/bar/`. If the base address path had ended with `/`, then the path would be `/foo/bar/baz/`. When looking for the service shared memory object, the full path is initally used. If it's not found, then the last segment is removed and the shared memory ojbect is looked for again. This process repeats until it gets down to `/` and it's not found.  
By default the first service found is the one connected to. A consequence of this algorithm is that a service using the GLOBAL namespace which has a base address path which is a base address of a service using the LOCAL namespace with a longer path will hide the service with the longer path as the GLOBAL namespace will be found first. E.g. if you have a service launched as an admin for the base address `net.pipe://localhost/foo/`, and a second service launched as a non-admin user for the base address `net.pipe://localhost/foo/bar/`, then clients looking for `net.pipe://localhost/foo/bar/` will find the admin GLOBAL shared memory object first as `/foo/` is a base path of `/foo/bar/`. There is an app context switch available to modify this search algorithm. With the modified search algorithm, it doesn't stop at the first found service but keeps searching and chooses the shared memory object which has the longest matching path. As `/foo/bar/` is longer than `/foo/`, despite being in the LOCAL namespace, that is the one which will get picked. You can switch to this best match algorithm by setting the app context switch `wcf:useBestMatchNamedPipeUri` to `true`. For .NET Framework, the easiest way is to add this to your app.config file.
```xml
<configuration>
  <appSettings>
    <add key="wcf:useBestMatchNamedPipeUri" value="true" />
  </appSettings>
</configuration>
```
For the base address `net.pipe://localhost/foo/bar/baz`, this is the order that the search will happen:
| Namespace | Hostname | Path |
| ---- | ---- | ---- |
| GLOBAL | + | /foo/bar/ |
| GLOBAL | + | /foo/ |
| GLOBAL | + | / |
| LOCAL | + | /foo/bar/ |
| LOCAL | + | /foo/ |
| LOCAL | + | / |
| GLOBAL | localhost | /foo/bar/ |
| GLOBAL | localhost | /foo/ |
| GLOBAL | localhost | / |
| LOCAL | localhost | /foo/bar/ |
| LOCAL | localhost | /foo/ |
| LOCAL | localhost | / |
| GLOBAL | * | /foo/bar/ |
| GLOBAL | * | /foo/ |
| GLOBAL | * | / |
| LOCAL | * | /foo/bar/ |
| LOCAL | * | /foo/ |
| LOCAL | * | / |

### Using the tool
Included in this repo is an example WCF service which uses the base path `net.pipe://localhost/MyService/`, and adds a service endpoint specifying the relative path `pipe`. You simply call `NetNamedPipePathSearcher` passing the Uri that the client would use (the `EndpointAddress` passed to `ChannelFactory<TChannel>`) to connect to the service. In this case, you would invoke it like this:
```cmd
> NetNamedPipePathSearcher net.pipe://localhost/MyService/pipe
```
The output will display all the paths that it searches. A `+` marker is emitted against the first endpoint found which is considered a match and has a service listening. This is the endpoint that a client would normally connect to. A second `#` marker is emitted against the endpoint which would be selected if you were to use the `wcf:useBestMatchNamedPipeUri` app context switch. Here is the output for the test service when another service is running that was launched as an admin and is using the base address `net.pipe://localhost/`.
```cmd
   HostName  ComparisonMode   Path             Global SharedMemoryName                                                  SMFound PipeName
   +         StrongWildcard   /MYSERVICE/PIPE/ True   Global\net.pipe:EbmV0LnBpcGU6Ly8rL01ZU0VSVklDRS9QSVBFLw==         False   N/A
   +         StrongWildcard   /MYSERVICE/      True   Global\net.pipe:EbmV0LnBpcGU6Ly8rL01ZU0VSVklDRS8=                 False   N/A
+  +         StrongWildcard   /                True   Global\net.pipe:EbmV0LnBpcGU6Ly8rLw==                             True    \\.\pipe\fab41af6-2a85-4bdc-99b2-6b1c72de97c1
   +         StrongWildcard   /MYSERVICE/PIPE/ False  Local\net.pipe:EbmV0LnBpcGU6Ly8rL01ZU0VSVklDRS9QSVBFLw==          False   N/A
#  +         StrongWildcard   /MYSERVICE/      False  Local\net.pipe:EbmV0LnBpcGU6Ly8rL01ZU0VSVklDRS8=                  True    \\.\pipe\bd553326-60ab-4295-9714-02b9b19cc8d1
   +         StrongWildcard   /                False  Local\net.pipe:EbmV0LnBpcGU6Ly8rLw==                              False   N/A
   localhost Exact            /MYSERVICE/PIPE/ True   Global\net.pipe:EbmV0LnBpcGU6Ly9MT0NBTEhPU1QvTVlTRVJWSUNFL1BJUEUv False   N/A
   localhost Exact            /MYSERVICE/      True   Global\net.pipe:EbmV0LnBpcGU6Ly9MT0NBTEhPU1QvTVlTRVJWSUNFLw==     False   N/A
   localhost Exact            /                True   Global\net.pipe:EbmV0LnBpcGU6Ly9MT0NBTEhPU1Qv                     False   N/A
   localhost Exact            /MYSERVICE/PIPE/ False  Local\net.pipe:EbmV0LnBpcGU6Ly9MT0NBTEhPU1QvTVlTRVJWSUNFL1BJUEUv  False   N/A
   localhost Exact            /MYSERVICE/      False  Local\net.pipe:EbmV0LnBpcGU6Ly9MT0NBTEhPU1QvTVlTRVJWSUNFLw==      False   N/A
   localhost Exact            /                False  Local\net.pipe:EbmV0LnBpcGU6Ly9MT0NBTEhPU1Qv                      False   N/A
   *         WeakWildcard     /MYSERVICE/PIPE/ True   Global\net.pipe:EbmV0LnBpcGU6Ly8qL01ZU0VSVklDRS9QSVBFLw==         False   N/A
   *         WeakWildcard     /MYSERVICE/      True   Global\net.pipe:EbmV0LnBpcGU6Ly8qL01ZU0VSVklDRS8=                 False   N/A
   *         WeakWildcard     /                True   Global\net.pipe:EbmV0LnBpcGU6Ly8qLw==                             False   N/A
   *         WeakWildcard     /MYSERVICE/PIPE/ False  Local\net.pipe:EbmV0LnBpcGU6Ly8qL01ZU0VSVklDRS9QSVBFLw==          False   N/A
   *         WeakWildcard     /MYSERVICE/      False  Local\net.pipe:EbmV0LnBpcGU6Ly8qL01ZU0VSVklDRS8=                  False   N/A
   *         WeakWildcard     /                False  Local\net.pipe:EbmV0LnBpcGU6Ly8qLw==                              False   N/A
# - Best match when using wcf:useBestMatchNamedPipeUri
+ - First match
```
Here is a description of each of the columns.
| Column Name | Description |
| ---- | ---- |
| HostName | The hostname that was used based on the `HostNameComparisonMode` |
| ComparisonMode | The `HostNameComparisonMode` that was searched for |
| Path | The path component used to derive the shared memory name |
| Global| `True` is searching the GLOBAL namespace, `False` if searching the local namespace |
| SharedMemoryName | The name of the kernel shared memory object that would be used to hold the named pipe name |
| SMFound | `True` if a kernel object exists and can be opened with the name specified in SharedMemoryName |
| PipeName | If the SharedMemoryName kernel object exists, this is the path to the named pipe object that it referenced|

In this output, the third entry (marked with `+`) is what would normally be selected by the client, and isn't the endpoint we want to connect to. The fifth entry (marked with a `#`) is what would be selected if we set `wcf:useBestMatchNamedPipeUri` to true, and in this case what we want to do.

### Use as a library
Although this tool is an .exe, it has has been designed to be usable as a library. It contains a public class `NetNamedPipePathSearcher.PathSearcher` with 2 static overloads of the method `SearchPath`. The first takes a `Uri` that you wish to search for. This creates a default `ILogger` which logs to the console, and only logs at the `Debug` level or higher. If there are no errors, this shouldn't log anything. If any exceptions are thrown, you should get `Warning` or high entries logged. If you wish to control logging, the second overload takes a `Uri` and an `ILogger` enabling you to log how you wish. This would be useful for example if you want to embed some diagnostics capability into a UI app in production code. There are many `Trace` level entries logged with fine grained information. The `SearchPath` methods return a `List<SearchedEndpoint>` which contains all the information that was displayed in the output when used as an executable at the command line.