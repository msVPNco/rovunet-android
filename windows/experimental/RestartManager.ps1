# Discovery only. Never invokes RmShutdown, RmRestart or broad process termination.
if(-not ('RvRestartManager' -as [type])) {
 Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class RvRestartManager {
 [StructLayout(LayoutKind.Sequential)] struct Unique { public int Pid;public System.Runtime.InteropServices.ComTypes.FILETIME Started; }
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct ProcessInfo {
  public Unique Process;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string AppName;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string Service;
  public uint Type,Status,Session;
  [MarshalAs(UnmanagedType.Bool)]public bool Restartable;
 }
 [DllImport("rstrtmgr.dll",CharSet=CharSet.Unicode)]static extern int RmStartSession(out uint session,uint flags,StringBuilder key);
 [DllImport("rstrtmgr.dll",CharSet=CharSet.Unicode)]static extern int RmRegisterResources(uint session,uint count,string[] files,uint apps,IntPtr processes,uint services,IntPtr serviceNames);
 [DllImport("rstrtmgr.dll")]static extern int RmGetList(uint session,out uint needed,ref uint count,[In,Out]ProcessInfo[] processes,ref uint reboot);
 [DllImport("rstrtmgr.dll")]static extern int RmEndSession(uint session);
 static void Check(int status){if(status!=0)throw new System.ComponentModel.Win32Exception(status);}
 public static int[] Owners(string[] files) {
  if(files.Length==0)return new int[0];if(files.Length>2048)throw new ArgumentException("Too many owned images");
  uint session;Check(RmStartSession(out session,0,new StringBuilder(33)));
  try {
   Check(RmRegisterResources(session,(uint)files.Length,files,0,IntPtr.Zero,0,IntPtr.Zero));
   uint needed=0,count=0,reboot=0;int status=RmGetList(session,out needed,ref count,new ProcessInfo[0],ref reboot);
   for(int retry=0;status==234&&retry<3;retry++){
    if(needed>4096)throw new InvalidOperationException("Resource owner limit");
    count=needed;var processes=new ProcessInfo[count];status=RmGetList(session,out needed,ref count,processes,ref reboot);
    if(status==0){var ids=new int[count];for(int i=0;i<count;i++)ids[i]=processes[i].Process.Pid;return ids;}
   }
   Check(status);return new int[0];
  }finally{Check(RmEndSession(session));}
 }
}
'@
}
