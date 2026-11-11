package android.util
object Base64 {
 const val NO_WRAP=2;const val NO_PADDING=1;const val URL_SAFE=8
 fun decode(s:String,flags:Int)=(if(flags and URL_SAFE!=0)java.util.Base64.getUrlDecoder() else java.util.Base64.getDecoder()).decode(s)
 fun encodeToString(b:ByteArray,flags:Int):String { val enc=java.util.Base64.getEncoder();return (if(flags and NO_PADDING!=0)enc.withoutPadding() else enc).encodeToString(b) }
}
