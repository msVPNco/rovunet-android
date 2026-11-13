using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
if(args.Length is not(4 or 5)||args[0] is not("protect" or "unprotect")||args[1] is not("native" or "dotnet")||!Path.IsPathFullyQualified(args[2])||args.Length==5&&!Path.IsPathFullyQualified(args[4])||!System.Text.RegularExpressions.Regex.IsMatch(args[3],"^[A-Za-z0-9_-]{1,50}$"))return 12;
string mode=args[0],backend=args[1],root=args[2],id=args[3];
Directory.CreateDirectory(root);string blobPath=Path.Combine(root,id+".blob"),metaPath=Path.Combine(root,id+".expected.json");
if(mode=="protect"&&(File.Exists(blobPath)||File.Exists(metaPath)))return 13;
string resultRoot=args.Length==5?args[4]:root;Directory.CreateDirectory(resultRoot);
var report=new Dictionary<string,object?>{["case"]=id,["mode"]=mode,["backend"]=backend,["before"]=Native.Identity(),["scope"]="CurrentUser",["nativeFlags"]=backend=="native"?1:null,["dotnetNativeFlagsObserved"]=false,["framework"]=RuntimeInformation.FrameworkDescription,["utc"]=DateTimeOffset.UtcNow,["blobPath"]=blobPath};
byte[]? plaintext=null;byte[]? blob=null;string expected="";bool passed=false;
try{
 if(mode=="protect"){
  plaintext=RandomNumberGenerator.GetBytes(64);expected=Convert.ToHexString(SHA256.HashData(plaintext));
  report["identityAtManagedCall"]=Native.Identity();
  if(backend=="native")blob=Native.Transform(plaintext,true,report);else blob=ProtectedData.Protect(plaintext,null,DataProtectionScope.CurrentUser);
  File.WriteAllBytes(blobPath,blob);var acl=new FileSecurity();var sid=WindowsIdentity.GetCurrent().User!;acl.SetOwner(sid);acl.SetAccessRuleProtection(true,false);acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,AccessControlType.Allow));new FileInfo(blobPath).SetAccessControl(acl);
  File.WriteAllText(metaPath,JsonSerializer.Serialize(new{expectedHash=expected,length=plaintext.Length,protectBackend=backend}));
  report["blobHashBefore"]=Convert.ToHexString(SHA256.HashData(blob));passed=true;
 }else{
  using var meta=JsonDocument.Parse(File.ReadAllBytes(metaPath));expected=meta.RootElement.GetProperty("expectedHash").GetString()!;
  blob=File.ReadAllBytes(blobPath);report["fileReadable"]=true;report["blobHashBefore"]=Convert.ToHexString(SHA256.HashData(blob));
  report["identityAtManagedCall"]=Native.Identity();plaintext=backend=="native"?Native.Transform(blob,false,report):ProtectedData.Unprotect(blob,null,DataProtectionScope.CurrentUser);
  passed=Convert.ToHexString(SHA256.HashData(plaintext))==expected;report["plaintextLength"]=plaintext.Length;
 }
}catch(Exception e){report["error"]=e.GetType().Name;report["hresult"]=e.HResult;}
finally{
 report["after"]=Native.Identity();report["passed"]=passed;report["expectedHash"]=expected;
 try{report["blobHashAfter"]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(blobPath)));}catch{report["blobHashAfter"]="not-readable";}
 if(plaintext!=null)CryptographicOperations.ZeroMemory(plaintext);
}
File.WriteAllText(Path.Combine(resultRoot,id+"."+mode+"."+backend+".result.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
return passed?0:73;

static class Native
{
 [StructLayout(LayoutKind.Sequential)]struct Blob{public int Length;public IntPtr Data;}
 [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]static extern bool CryptProtectData(ref Blob input,string? description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
 [DllImport("crypt32.dll",SetLastError=true)]static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
 [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr memory);
 [DllImport("advapi32.dll",SetLastError=true)]static extern bool OpenProcessToken(IntPtr process,uint rights,out IntPtr token);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr value);
 public static object Identity(){
  string? processSid=null;
  if(OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle,8,out var token)){try{using var identity=new WindowsIdentity(token);processSid=identity.User?.Value;}finally{CloseHandle(token);}}
  using var current=WindowsIdentity.GetCurrent();using var thread=WindowsIdentity.GetCurrent(true);
  return new{processSid,effectiveSid=current.User?.Value,threadSid=thread?.User?.Value,impersonation=current.ImpersonationLevel.ToString(),profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),profileLoaded=Microsoft.Win32.Registry.Users.OpenSubKey(current.User!.Value)!=null};
 }
 public static byte[] Transform(byte[] bytes,bool protect,Dictionary<string,object?> report){
  var input=new Blob{Length=bytes.Length,Data=Marshal.AllocHGlobal(bytes.Length)};Blob output=default;
  try{
   Marshal.Copy(bytes,0,input.Data,bytes.Length);report["identityAtNativeCall"]=Identity();
   Marshal.SetLastPInvokeError(0);
   bool ok=protect?CryptProtectData(ref input,null,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
   int error=Marshal.GetLastPInvokeError(); // Immediately, before another API/identity lookup.
   report["nativeSucceeded"]=ok;report["nativeError"]=ok?0:error;
   if(!ok)throw new System.ComponentModel.Win32Exception(error);
   var result=new byte[output.Length];Marshal.Copy(output.Data,result,0,result.Length);return result;
  }finally{if(output.Data!=IntPtr.Zero)LocalFree(output.Data);Marshal.FreeHGlobal(input.Data);}
 }
}
