package app.slipnet.util
object AppLog {
 @Volatile var errorHook: ((String) -> Unit)? = null
 @Volatile var debugHook: ((String, String) -> Unit)? = null
 fun d(tag:String,msg:String) { debugHook?.invoke(tag,msg) }
 fun i(tag:String,msg:String)=Unit;fun w(tag:String,msg:String)=Unit
 fun w(tag:String,msg:String,e:Throwable)=Unit
 fun e(tag:String,msg:String)=Unit
 fun e(tag:String,msg:String,e:Throwable) { errorHook?.invoke(tag) }
}
