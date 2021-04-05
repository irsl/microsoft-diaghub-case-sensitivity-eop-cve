using DiagnosticsHub.StandardCollector.Host.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DiagnosticsHub.StandardCollector
{

        [TypeLibType(TypeLibTypeFlags.FCanCreate)]
        [ClassInterface(ClassInterfaceType.None)]
        [Guid("42CBFAA7-A4A7-47BB-B422-BD10E9D02700")]
        [ComImport]
        public class StandardCollectorNativeClass : IStandardCollectorServiceNative
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            public virtual extern ICollectionSessionNative CreateSession(ref SessionConfiguration sessionConfig, IStandardCollectorClientDelegateNative clientDelegate);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            public virtual extern ICollectionSessionNative GetSession(ref Guid SessionId);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            public virtual extern void DestroySession(ref Guid SessionId);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            public virtual extern void DestroySessionAsync(ref Guid SessionId);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            public virtual extern void AddLifetimeMonitorProcessIdForSession(ref Guid SessionId, uint LifetimeMonitorProcessId);
        }


        [Guid("60a2c2a0-bb00-48b6-b6ac-7be5f3211af5")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ICollectionSessionNative : IStandardCollectorMessagePort
        {
            void PostStringToListener(Guid listenerId, string payload);
            void PostBytesToListener(Guid listenerId, ref CollectorByteMessage payload);
            void AddAgent(string agentName, ref Guid clsid);
            object Start();
            object GetCurrentResult(bool pauseCollection);
            void Pause();
            void Resume();
            object Stop();
            void TriggerEvent(SessionEvent eventType, ref object eventArg1, ref object eventArg2, out object eventOut);
            GraphDataUpdates GetGraphDataUpdates(ref Guid agentId, string[] counterIdAsBstrs);
            SessionState QueryState();
            string GetStatusChangeEventName();
            int GetLastError();
            object SetClientDelegate(IStandardCollectorClientDelegateNative clientDelegate);
            void AddAgentWithConfiguration(string agentName, ref Guid clsid, string agentConfiguration);
        }

    [ComImport]
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    [Guid("4323664b-b884-4929-8377-d2fd097f7bd3")]
        public interface IStandardCollectorClientDelegateNative
        {
            void OnReceiveString(ref Guid listenerId, string payload);
            void OnReceiveBytes(ref Guid listenerId, ref CollectorByteMessage payload);
            void OnReceiveFile(ref Guid listenerId, string localFilePath, bool deleteAfterPost);
        }


        [Guid("0D8AF6B7-EFD5-4F6D-A834-314740AB8CAA")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IStandardCollectorServiceNative
        {
            ICollectionSessionNative CreateSession(ref SessionConfiguration sessionConfig, IStandardCollectorClientDelegateNative clientDelegate);
            ICollectionSessionNative GetSession(ref Guid SessionId);
            void DestroySession(ref Guid SessionId);
            void DestroySessionAsync(ref Guid SessionId);
            void AddLifetimeMonitorProcessIdForSession(ref Guid SessionId, uint LifetimeMonitorProcessId);
        }

}
