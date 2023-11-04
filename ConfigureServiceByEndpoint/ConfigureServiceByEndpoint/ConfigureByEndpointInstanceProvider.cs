using CoreWCF.Dispatcher;
using System.Collections.ObjectModel;

namespace ConfigureServiceByEndpoint
{
    public class ConfigureByEndpointInstanceProvider : IInstanceProvider
    {
        private readonly IServiceProvider _serviceProvider;

        public ConfigureByEndpointInstanceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public object GetServiceInstanceByPath(string requestPath, IServiceProvider serviceProvider)
        {
            ILogger logger;
            string prefix;
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            switch (requestPath)
            {
                case "/MyService_Development.svc":
                    logger = loggerFactory.CreateLogger("MyService_Development");
                    prefix = "DEV";
                    break;
                case "/MyService_Staging.svc":
                    logger = loggerFactory.CreateLogger("MyService_Staging");
                    prefix = "STAGING";
                    break;
                case "/MyService_Production.svc":
                    logger = loggerFactory.CreateLogger("MyService_Production");
                    prefix = "PROD";
                    break;
                default:
                    logger = serviceProvider.GetRequiredService<ILogger<Service>>();
                    prefix = "UNKNOWN";
                    break;
            }

            // Could also add to DI and use ActivatorUtilities.CreateInstance instead.
            // If you do use ActivatorUtilities.CreateInstance, then remove the Dispose call from ReleaseInstance
            return new Service(logger, prefix);
        }

        public object GetInstance(InstanceContext instanceContext)
        {
            // Needed by some authorization initialization logic to discover the concrete type and any Authorization attributes
            // that need to be applied. If you aren't using authn/authz, this can return null. Best to return an instance of the
            // class just in case. No service methods will be called on it.
            return new Service();
        }

        public object GetInstance(InstanceContext instanceContext, Message message)
        {
            var extension = GetScopedServiceProviderExtension(instanceContext);
            if (extension == null)
            {
                extension = new ScopedServiceProviderExtension(_serviceProvider);
                instanceContext.Extensions.Add(extension);
            }

            // Only safe to do with BasicHttpBinding. With WSHttpBinding and NetHttpBinding, the client controls
            // the To header. If not using BasicHttpBinding, get the HttpRequest that CoreWCF saved
            // to message.Properties and extract the Url from there.
            var requestPath = message.Headers.To.AbsolutePath;

            // Need to use extension as IServiceProvider as it creates a scope
            var instance = GetServiceInstanceByPath(requestPath, extension);
            return instance;
        }

        public void ReleaseInstance(InstanceContext instanceContext, object instance)
        {
            var extension = GetScopedServiceProviderExtension(instanceContext);
            instanceContext.Extensions.Remove(extension);
            extension.Dispose();
            // If the service instance is created by DI, remove the following line as
            // disposing of the IServiceScope inside of extension will do that for you
            if (instance is IDisposable disposable) { disposable.Dispose(); }
        }

        private static ScopedServiceProviderExtension GetScopedServiceProviderExtension(InstanceContext instanceContext)
            => instanceContext.Extensions.Find<ScopedServiceProviderExtension>();

        // This class is needed to be attached to the InstanceContext to make DI injection into operation methods work
        private class ScopedServiceProviderExtension : IExtension<InstanceContext>, IServiceProvider, IDisposable
        {
            private readonly IServiceScope _serviceScope;

            public ScopedServiceProviderExtension(IServiceProvider serviceProvider)
                => _serviceScope = serviceProvider.CreateScope();

            public void Attach(InstanceContext owner)
            {
                // intentionally left blank
            }

            public void Detach(InstanceContext owner)
            {
                // intentionally left blank
            }

            public object? GetService(Type serviceType) => _serviceScope.ServiceProvider.GetService(serviceType);

            public void Dispose() => _serviceScope?.Dispose();
        }
    }

    internal class InstanceProviderServiceBehavior<T> : IServiceBehavior where T : IInstanceProvider
    {
        private readonly T _instanceProvider;

        public InstanceProviderServiceBehavior(T instanceProvider)
        {
            _instanceProvider = instanceProvider;
        }

        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters) { }

        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            foreach (ChannelDispatcherBase cdb in serviceHostBase.ChannelDispatchers)
            {
                var dispatcher = (ChannelDispatcher)cdb;
                foreach (EndpointDispatcher endpointDispatcher in dispatcher.Endpoints)
                {
                    if (!endpointDispatcher.IsSystemEndpoint)
                    {
                        if (_instanceProvider != null)
                        {
                            endpointDispatcher.DispatchRuntime.InstanceProvider = _instanceProvider;
                        }
                    }
                }
            }
        }

        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase) { }
    }
}
