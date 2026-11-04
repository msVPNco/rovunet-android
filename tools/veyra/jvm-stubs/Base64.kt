package android.util
object Base64 {
 const val NO_WRAP = 2
 const val URL_SAFE = 8
 fun decode(s: String, flags: Int): ByteArray = (if (flags and URL_SAFE != 0) java.util.Base64.getUrlDecoder() else java.util.Base64.getDecoder()).decode(s)
 fun encodeToString(b: ByteArray, flags: Int): String = (if (flags and URL_SAFE != 0) java.util.Base64.getUrlEncoder() else java.util.Base64.getEncoder()).encodeToString(b)
}
