// Decompiled with JetBrains decompiler
// Type: Microsoft.DiagnosticsHub.StandardCollector.VspxConverter
// Assembly: VSDiagnostics, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2F9D4495-41C9-4FD5-8B64-0B1B63E216DA
// Assembly location: C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Team Tools\DiagnosticsHub\Collector\VSDiagnostics.exe

using DiagnosticsHub.Packaging.Interop;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Microsoft.DiagnosticsHub.StandardCollector
{
  internal sealed class VspxConverter : IDisposable
  {
    private static readonly Guid CpuUsageToolGuid = new Guid("96f1f3e8-f762-4cd2-8ed9-68ec25c2c722");
    private static readonly Guid JavaScriptFunctionTimingToolGuid = new Guid("22dbda0b-15e0-4c3e-b883-429c784837c3");
    private static readonly string VspxExtension = ".vspx";
    private readonly IDhPackage package;
    private readonly IList<string> tempPaths;
    private bool isDisposed;

    public VspxConverter(IDhPackage package)
    {
      if (package == null)
        throw new ArgumentException(nameof (package));
      this.package = package;
      this.tempPaths = (IList<string>) new List<string>();
    }

    public void Dispose()
    {
      if (this.isDisposed)
        return;
      foreach (string tempPath in (IEnumerable<string>) this.tempPaths)
      {
        if (Directory.Exists(tempPath))
          Directory.Delete(tempPath, true);
        else if (File.Exists(tempPath))
          File.Delete(tempPath);
      }
      this.isDisposed = true;
    }

    public string Convert(string vspxReport)
    {
      if (string.IsNullOrEmpty(vspxReport) || File.Exists(vspxReport))
        throw new ArgumentException(string.Format((IFormatProvider) CultureInfo.CurrentCulture, Resources.ErrMsg_InvalidDocumentConversionDestination, (object) vspxReport), nameof (vspxReport));
      if (!VspxConverter.VspxExtension.Equals(Path.GetExtension(vspxReport), StringComparison.OrdinalIgnoreCase))
        vspxReport += VspxConverter.VspxExtension;
      string vspxTaskName = (string) null;
      ToolInfo[] toolInfos;
      this.package.GetTools(out toolInfos);
      foreach (ToolInfo toolInfo in toolInfos)
      {
        Guid toolId = toolInfo.ToolId;
        if (toolId == VspxConverter.CpuUsageToolGuid)
        {
          vspxTaskName = "CPU";
          break;
        }
        if (toolId == VspxConverter.JavaScriptFunctionTimingToolGuid)
        {
          vspxTaskName = "Timing";
          break;
        }
      }
      if (string.IsNullOrEmpty(vspxTaskName))
        throw new Exception(Resources.ErrMsg_DocumentContainsNoConvertableTools);
      ResourceInfo[] resourceInfos;
      this.package.GetResourceInformationForAll(out resourceInfos);
      List<ResourceInfo> resourceInfoList = new List<ResourceInfo>();
      foreach (ResourceInfo resourceInfo in resourceInfos)
      {
        if (resourceInfo.ResourceType.Equals("DiagnosticsHub.Resource.EtlFile") || resourceInfo.ResourceType.Equals("DiagnosticsHub.Resource.JavaScript.SourceDirectory") || resourceInfo.ResourceType.Equals("DiagnosticsHub.Resource.SymbolCache"))
          resourceInfoList.Add(resourceInfo);
      }
      if (resourceInfoList.Count == 0)
        throw new Exception(Resources.ErrMsg_DocumentContainsNoConvertableData);
      using (FileStream fileStream = File.Open(vspxReport, FileMode.CreateNew, FileAccess.Write, FileShare.None))
      {
        using (ZipArchive vspxArchive = new ZipArchive((Stream) fileStream, ZipArchiveMode.Create, false))
        {
          foreach (ResourceInfo resourceInfo in resourceInfoList)
          {
            if (resourceInfo.ResourceType.Equals("DiagnosticsHub.Resource.EtlFile"))
              this.AddEtlFileToVspxArchive(vspxArchive, resourceInfo.ResourceId);
            else if (resourceInfo.ResourceType.Equals("DiagnosticsHub.Resource.JavaScript.SourceDirectory"))
              this.AddJavaScriptSourceToVspxArchive(vspxArchive, resourceInfo.ResourceId);
            else if (resourceInfo.ResourceType.Equals("DiagnosticsHub.Resource.SymbolCache"))
              this.AddSymbolCacheToVspxArchive(vspxArchive, resourceInfo.ResourceId);
          }
          VspxConverter.WriteMetadataInVspxArchive(vspxArchive, vspxTaskName);
        }
      }
      return vspxReport;
    }

    private static void WriteMetadataInVspxArchive(ZipArchive vspxArchive, string vspxTaskName)
    {
      using (StreamWriter streamWriter = new StreamWriter(vspxArchive.CreateEntry("VSProfilingData\\Metadata\\ReportMetadata.xsd", CompressionLevel.Fastest).Open()))
        streamWriter.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<xs:schema\r\n    xmlns:xs=\"http://www.w3.org/2001/XMLSchema\"\r\n    xmlns=\"http://schemas.microsoft.com/vs/profiler/ReportMetadata/1.0\" \r\n    targetNamespace=\"http://schemas.microsoft.com/vs/profiler/ReportMetadata/1.0\" \r\n    elementFormDefault=\"unqualified\" \r\n    attributeFormDefault=\"unqualified\">\r\n  <xs:complexType name=\"MetadataType\">\r\n   <xs:sequence>\r\n      <xs:element name=\"TaskName\" type=\"xs:string\"  minOccurs=\"1\" maxOccurs=\"unbounded\"/>\r\n    </xs:sequence>\r\n  </xs:complexType>\r\n  <xs:element name=\"Metadata\" type=\"MetadataType\" />\r\n</xs:schema>");
      using (StreamWriter streamWriter = new StreamWriter(vspxArchive.CreateEntry("VSProfilingData\\Metadata\\vsperfmetadata.xml", CompressionLevel.Fastest).Open()))
        streamWriter.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<Metadata xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.microsoft.com/vs/profiler/ReportMetadata/1.0\">\r\n  <TaskName xmlns=\"\">" + vspxTaskName + "</TaskName>\r\n</Metadata>");
    }

    private string GetResourcePath(Guid resourceId)
    {
      bool canBeReadOnly = true;
      string str = this.package.GetResourcePath(ref resourceId, ref canBeReadOnly);
      if (!canBeReadOnly)
      {
        string extractionRootPath = Path.GetTempPath() + resourceId.ToString("N");
        this.tempPaths.Add(extractionRootPath);
        str = this.package.ExtractResourceToRootPath(ref resourceId, extractionRootPath);
      }
      return str;
    }

    private void AddEtlFileToVspxArchive(ZipArchive vspxArchive, Guid resourceId)
    {
      string resourcePath = this.GetResourcePath(resourceId);
      using (FileStream fileStream = File.OpenRead(resourcePath))
      {
        string entryName = string.Format("VSProfilingData\\{0}", (object) Path.GetFileName(resourcePath));
        using (Stream destination = vspxArchive.CreateEntry(entryName, CompressionLevel.NoCompression).Open())
          fileStream.CopyTo(destination);
      }
    }

    private void AddJavaScriptSourceToVspxArchive(ZipArchive vspxArchive, Guid resourceId)
    {
      foreach (string enumerateFile in Directory.EnumerateFiles(this.GetResourcePath(resourceId), "*.js", SearchOption.AllDirectories))
      {
        using (FileStream fileStream = File.OpenRead(enumerateFile))
        {
          string entryName = string.Format("VSProfilingData\\JavaScriptSource\\{0}", (object) Path.GetFileName(enumerateFile));
          using (Stream destination = vspxArchive.CreateEntry(entryName, CompressionLevel.NoCompression).Open())
            fileStream.CopyTo(destination);
        }
      }
    }

    private void AddSymbolCacheToVspxArchive(ZipArchive vspxArchive, Guid resourceId)
    {
      foreach (string enumerateFile in Directory.EnumerateFiles(this.GetResourcePath(resourceId), "*.pdb", SearchOption.AllDirectories))
      {
        SafeFileHandle file = VspxConverter.NativeMethods.CreateFile("\\\\?\\" + enumerateFile, 2147483648U, 1U, IntPtr.Zero, 4U, 128U, IntPtr.Zero);
        if (!file.IsInvalid)
        {
          using (FileStream fileStream = new FileStream(file, FileAccess.Read))
          {
            string fileName = Path.GetFileName(enumerateFile);
            int startIndex = enumerateFile.LastIndexOf("SymCache", StringComparison.OrdinalIgnoreCase);
            string entryName = startIndex != -1 ? enumerateFile.Substring(startIndex) : string.Format("SymCache\\{0}", (object) fileName);
            using (Stream destination = vspxArchive.CreateEntry(entryName, CompressionLevel.NoCompression).Open())
              fileStream.CopyTo(destination);
          }
        }
      }
    }

    private static class NativeMethods
    {
      public const uint GENERIC_READ = 2147483648;
      public const uint FILE_SHARE_READ = 1;
      public const uint OPEN_ALWAYS = 4;
      public const uint FILE_ATTRIBUTE_NORMAL = 128;

      [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
      internal static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);
    }
  }
}
