using System.Globalization;

namespace Rovunet.Core;

public static partial class CoreLinks
{
    // Validate the client form supported by pinned Xray a4a3dff. Never replace an
    // encrypted profile with plaintext, and never include key material in errors.
    public static string NormalizeVlessEncryption(string value)
    {
        if(value is "" or "none")return "none";
        var parts=value.Split('.');if(parts[0]!="mlkem768x25519plus")throw new SafeError("UNSUPPORTED_FORMAT");
        void Invalid()=>throw new SafeError("VLESS_ENCRYPTION_INVALID");
        if(value.Length>65536||parts.Length<4||parts[1] is not("native" or "xorpub" or "random")||parts[2] is not("0rtt" or "1rtt"))Invalid();
        bool keys=false;int keyCount=0,paddingCount=0;long paddingLength=0;
        foreach(string part in parts.Skip(3)){
            if(part.Length<20){
                if(keys)Invalid();var triple=part.Split('-');if(triple.Length!=3)Invalid();var numbers=new int[3];
                for(int i=0;i<3;i++)if(!int.TryParse(triple[i],NumberStyles.None,CultureInfo.InvariantCulture,out numbers[i]))Invalid();
                if(paddingCount==0&&(numbers[0]<100||numbers[1]<35||numbers[2]<35))Invalid();
                if(paddingCount++%2==0)paddingLength+=Math.Max(numbers[1],numbers[2]);if(paddingLength>65553)Invalid();continue;
            }
            keys=true;if(part.Length>1580||part.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not('-' or '_')))Invalid();
            byte[] bytes;try{string encoded=part.Replace('-','+').Replace('_','/');bytes=Convert.FromBase64String(encoded.PadRight((encoded.Length+3)/4*4,'='));}catch{throw new SafeError("VLESS_ENCRYPTION_INVALID");}
            if(bytes.Length is not(32 or 1184))Invalid();keyCount++;
        }
        if(keyCount==0)Invalid();return value;
    }
}
