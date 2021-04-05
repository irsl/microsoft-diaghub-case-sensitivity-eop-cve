// Decompiled with JetBrains decompiler
// Type: Microsoft.DiagnosticsHub.StandardCollector.ClientDelegate
// Assembly: VSDiagnostics, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2F9D4495-41C9-4FD5-8B64-0B1B63E216DA
// Assembly location: C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Team Tools\DiagnosticsHub\Collector\VSDiagnostics.exe

using DiagnosticsHub.StandardCollector.Host.Interop;
using System;
using System.Runtime.InteropServices;

namespace Microsoft.DiagnosticsHub.StandardCollector
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [ComDefaultInterface(typeof(IStandardCollectorClientDelegateNative))]
    public class ClientDelegateNative : IStandardCollectorClientDelegateNative
    {
        [CLSCompliant(false)]
        public void OnReceiveBytes(ref Guid listenerId, ref CollectorByteMessage payload)
        {
            Console.WriteLine($"OnReceiveBytes!: {listenerId} payload length: {payload.Length}");
        }

        public void OnReceiveFile(ref Guid listenerId, string localFilePath, bool deleteAfterPost)
        {
            Console.WriteLine($"OnReceiveFile!: {listenerId} {localFilePath}");
            if (!(listenerId == Guid.Empty))
                return;
        }

        public void OnReceiveString(ref Guid listenerId, string payload)
        {
            Console.WriteLine($"OnReceiveString!: {listenerId} {payload}");
            if (!(listenerId == Guid.Empty))
                return;
            Console.WriteLine(payload);
        }
    }

}
