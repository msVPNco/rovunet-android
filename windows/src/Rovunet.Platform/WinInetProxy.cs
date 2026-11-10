using System.Runtime.InteropServices;
using Microsoft.Win32;
using Rovunet.Core;

namespace Rovunet.Platform;

public sealed class WinInetProxy : IProxyBackend
{
    [StructLayout(LayoutKind.Explicit)] struct Value { [FieldOffset(0)] public int Flags; [FieldOffset(0)] public IntPtr Text; }
    [StructLayout(LayoutKind.Sequential)] struct Option { public int Id; public Value Value; }
    [StructLayout(LayoutKind.Sequential)] struct OptionList { public int Size; public IntPtr Connection; public int Count; public int Error; public IntPtr Options; }
    [DllImport("wininet.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool InternetQueryOptionW(IntPtr handle, int option, ref OptionList value, ref int length);
    [DllImport("wininet.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool InternetSetOptionW(IntPtr handle, int option, IntPtr value, int length);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalFree(IntPtr p);
    public ProxySettings Read()
    {
        int size = Marshal.SizeOf<Option>(); IntPtr ptr = Marshal.AllocHGlobal(size * 4);
        try
        {
            for (int i = 0; i < 4; i++) Marshal.StructureToPtr(new Option { Id = i == 0 ? 10 : i + 1 }, ptr + i * size, false);
            var list = new OptionList { Size = Marshal.SizeOf<OptionList>(), Count = 4, Options = ptr }; int length = list.Size;
            if (!InternetQueryOptionW(IntPtr.Zero, 75, ref list, ref length)) throw new SafeError("PROXY_READ_FAILED");
            var flags = Marshal.PtrToStructure<Option>(ptr).Value.Flags;
            string ReadString(int i) { var p = Marshal.PtrToStructure<Option>(ptr + i * size).Value.Text; return p == IntPtr.Zero ? "" : Marshal.PtrToStringUni(p) ?? ""; }
            return new(flags, ReadString(1), ReadString(2), ReadString(3));
        }
        finally
        {
            for (int i = 1; i < 4; i++) { var p = Marshal.PtrToStructure<Option>(ptr + i * size).Value.Text; if (p != IntPtr.Zero) GlobalFree(p); }
            Marshal.FreeHGlobal(ptr);
        }
    }
    public void Write(ProxySettings settings)
    {
        int size = Marshal.SizeOf<Option>(); var allocated = new List<IntPtr>(); IntPtr ptr = Marshal.AllocHGlobal(size * 4), lp = Marshal.AllocHGlobal(Marshal.SizeOf<OptionList>());
        try
        {
            Marshal.StructureToPtr(new Option { Id = 1, Value = new() { Flags = settings.Flags } }, ptr, false);
            string[] strings = [settings.Server, settings.Bypass, settings.AutoConfigUrl];
            for (int i = 1; i < 4; i++) { var s = Marshal.StringToHGlobalUni(strings[i - 1]); allocated.Add(s); Marshal.StructureToPtr(new Option { Id = i + 1, Value = new() { Text = s } }, ptr + i * size, false); }
            var list = new OptionList { Size = Marshal.SizeOf<OptionList>(), Count = 4, Options = ptr }; Marshal.StructureToPtr(list, lp, false);
            if (!InternetSetOptionW(IntPtr.Zero, 75, lp, list.Size)) throw new SafeError("PROXY_WRITE_FAILED");
            if (!InternetSetOptionW(IntPtr.Zero, 39, IntPtr.Zero, 0) || !InternetSetOptionW(IntPtr.Zero, 37, IntPtr.Zero, 0)) throw new SafeError("PROXY_NOTIFY_FAILED");
        }
        finally { foreach (var p in allocated) Marshal.FreeHGlobal(p); Marshal.FreeHGlobal(ptr); Marshal.FreeHGlobal(lp); }
    }
}

public static class Recovery
{
    const string RunOncePath = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    // Read-only uninstall preflight: distinguish this installation's recovery from
    // another active version. Compare resolved file handles, including junction aliases.
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern uint GetFinalPathNameByHandleW(Microsoft.Win32.SafeHandles.SafeFileHandle file,System.Text.StringBuilder path,uint length,uint flags);
    public static int UninstallScope(ProxyJournal? journal,string directory,string armedCommand)
    {
        if(journal==null)return 0;
        try{
            string owner;
            if(!string.IsNullOrEmpty(journal.OwnerDirectory))owner=Path.Combine(journal.OwnerDirectory,"Rovunet.Agent.exe");
            else{
                var match=System.Text.RegularExpressions.Regex.Match(armedCommand,"^\"([^\"]+)\" --recover$",System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                if(!match.Success)return 12;owner=match.Groups[1].Value;
                if(!Path.GetFileName(owner).Equals("Rovunet.Agent.exe",StringComparison.OrdinalIgnoreCase))return 12;
            }
            string Resolve(string path){if(!Path.IsPathFullyQualified(path))throw new IOException();using var file=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);var buffer=new System.Text.StringBuilder(32768);uint size=GetFinalPathNameByHandleW(file.SafeFileHandle,buffer,(uint)buffer.Capacity,0);if(size==0||size>=buffer.Capacity)throw new IOException();return buffer.ToString();}
            return Resolve(Path.Combine(directory,"Rovunet.Agent.exe")).Equals(Resolve(owner),StringComparison.OrdinalIgnoreCase)?10:0;
        }catch{return 12;}
    }
    public static int UninstallScope()
    {
        using var key=Registry.CurrentUser.OpenSubKey(RunOncePath);
        return UninstallScope(new LeaseStore().Read(),AppContext.BaseDirectory,key?.GetValue("!RovunetRecovery") as string??"");
    }
    public static void Arm()
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunOncePath);
        k.SetValue("!RovunetRecovery", "\"" + Path.Combine(AppContext.BaseDirectory, "Rovunet.Agent.exe") + "\" --recover");
    }
    public static void Disarm() { using var k = Registry.CurrentUser.OpenSubKey(RunOncePath, true); k?.DeleteValue("!RovunetRecovery", false); }
    public static void KeepCurrent()
    {
        var store = new LeaseStore(); var journal = store.Read();
        if (journal == null) { Disarm(); return; }
        var current = new WinInetProxy().Read();
        if (current == journal.Applied) throw new SafeError("RECOVERY_REQUIRED");
        // Explicit user decision: preserve the external settings; do not write WinINET.
        store.Delete(); Disarm();
    }
    public static void Restore() { new ProxyLease(new WinInetProxy(), new LeaseStore()).Recover(); Disarm(); }
}
