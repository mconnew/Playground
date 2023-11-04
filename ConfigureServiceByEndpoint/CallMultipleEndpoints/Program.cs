using CallMultipleEndpoints;
using System.ServiceModel;

var factory = new ChannelFactory<IService>(new BasicHttpBinding(BasicHttpSecurityMode.Transport));
var devChannel = factory.CreateChannel(new EndpointAddress("https://localhost:7136/MyService_Development.svc"));
var stagingChannel = factory.CreateChannel(new EndpointAddress("https://localhost:7136/MyService_Staging.svc"));
var prodChannel = factory.CreateChannel(new EndpointAddress("https://localhost:7136/MyService_Production.svc"));
Console.WriteLine(devChannel.GetData(10));
Console.WriteLine(stagingChannel.GetData(11));
Console.WriteLine(prodChannel.GetData(12));
Console.ReadLine();