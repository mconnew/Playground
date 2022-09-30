using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading.Tasks;

namespace TrackHttpPayload
{
    internal class HttpWebRequestProxy : RealProxy, IRemotingTypeInfo
    {
        private readonly HttpWebRequest _httpWebRequest;
        private IHttpWebRequestInspector _callbackHandler;

        private static string[] s_getRequestStreamMethodNames =
        {
            nameof(HttpWebRequest.GetRequestStream),
            nameof(HttpWebRequest.BeginGetRequestStream),
            nameof(HttpWebRequest.GetRequestStreamAsync)
        };

        private static string[] s_getResponseMethodNames =
        {
            nameof(HttpWebRequest.GetResponse),
            nameof(HttpWebRequest.EndGetResponse)
        };

        public HttpWebRequestProxy(Uri uri, IHttpWebRequestInspector callbackHandler) : base(typeof(HttpWebRequest))
        {
            _httpWebRequest = (HttpWebRequest)WebRequest.CreateDefault(uri);
            _callbackHandler = callbackHandler;
        }

        bool IRemotingTypeInfo.CanCastTo(Type toType, object o)
        {
            return toType.IsAssignableFrom(typeof(HttpWebRequest));
        }

        string IRemotingTypeInfo.TypeName
        {
            get { return typeof(HttpWebRequest).FullName; }
            set { }
        }

        public override IMessage Invoke(IMessage message)
        {
            try
            {
                if (!(message is IMethodCallMessage methodCall))
                    throw new ArgumentException("Expected IMethodCallMessage");

                MethodBase method = methodCall.MethodBase;
                CallSendingRegisteredCallback(method);
                object[] args = methodCall.Args;
                var ret = method.Invoke(_httpWebRequest, args);

                var outputParamInfo = GetOutputParameters(method);
                var outArgs = new object[outputParamInfo.Length];
                for (int i = 0; i < outputParamInfo.Length; i++)
                {
                    int argPos = outputParamInfo[i].Position;
                    if (args[argPos] == null)
                    {
                        // the RealProxy infrastructure requires a default value for value types
                        outArgs[i] = GetDefaultParameterValue(GetParameterType(outputParamInfo[i]));
                    }
                    else
                    {
                        outArgs[i] = args[argPos];
                    }
                }

                ret = CallReceiveRegisteredCallback(method, ret);
                return new ReturnMessage(ret, outArgs, outArgs.Length, methodCall.LogicalCallContext, methodCall);
            }
            catch (Exception e)
            {
                return new ReturnMessage(e, message as IMethodCallMessage);
            }
        }

        private void CallSendingRegisteredCallback(MethodBase method)
        {
            if (s_getRequestStreamMethodNames.Contains(method.Name))
            {
                _callbackHandler.SendingRequest?.Invoke(_httpWebRequest);
            }
        }

        private object CallReceiveRegisteredCallback(MethodBase method, object retval)
        {
            if (s_getResponseMethodNames.Contains(method.Name) && retval is HttpWebResponse httpWebResponse)
            {
                _callbackHandler.ReceivingResponse?.Invoke(httpWebResponse);
                return httpWebResponse;
            }

            if (nameof(HttpWebRequest.GetResponseAsync).Equals(method.Name) &&
                retval is Task<HttpWebResponse> httpWebResponseTask)
            {
                return httpWebResponseTask.ContinueWith(antecedant =>
                {
                    HttpWebResponse response = antecedant.GetAwaiter().GetResult();
                    _callbackHandler.ReceivingResponse?.Invoke(response);
                    return response;
                });
            }

            return retval;
        }

        internal static object GetDefaultParameterValue(Type parameterType)
        {
            return (parameterType.IsValueType && parameterType != typeof(void)) ? Activator.CreateInstance(parameterType) : null;
        }

        internal static Type GetParameterType(ParameterInfo parameterInfo)
        {
            Type parameterType = parameterInfo.ParameterType;
            if (parameterType.IsByRef)
            {
                return parameterType.GetElementType();
            }
            else
            {
                return parameterType;
            }
        }

        static internal ParameterInfo[] GetOutputParameters(MethodBase method)
        {
            int count = 0;
            ParameterInfo[] parameters = method.GetParameters();

            // length of parameters we care about (-1 for async)
            int len = parameters.Length;

            // count the outs
            for (int i = 0; i < len; i++)
            {
                if (parameters[i].ParameterType.IsByRef)
                {
                    count++;
                }
            }

            // grab the outs
            ParameterInfo[] result = new ParameterInfo[count];
            int pos = 0;
            for (int i = 0; i < len; i++)
            {
                ParameterInfo param = parameters[i];
                if (param.ParameterType.IsByRef)
                {
                    result[pos++] = param;
                }
            }
            return result;
        }
    }
}