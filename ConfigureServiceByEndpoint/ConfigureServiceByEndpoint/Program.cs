var builder = WebApplication.CreateBuilder();

builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();
builder.Services.AddSingleton<ConfigureByEndpointInstanceProvider>();
builder.Services.AddSingleton<IServiceBehavior, InstanceProviderServiceBehavior<ConfigureByEndpointInstanceProvider>>();

var app = builder.Build();

app.UseServiceModel(serviceBuilder =>
{
    var binding = new BasicHttpBinding(BasicHttpSecurityMode.Transport);
    serviceBuilder.AddService<Service>(options =>
    {
        options.DebugBehavior.IncludeExceptionDetailInFaults = true;
    });
    serviceBuilder.AddServiceEndpoint<Service, IService>(binding, "/MyService_Development.svc");
    serviceBuilder.AddServiceEndpoint<Service, IService>(binding, "/MyService_Staging.svc");
    serviceBuilder.AddServiceEndpoint<Service, IService>(binding, "/MyService_Production.svc");
    var serviceMetadataBehavior = app.Services.GetRequiredService<ServiceMetadataBehavior>();
    serviceMetadataBehavior.HttpsGetEnabled = true;
});

app.Run();
