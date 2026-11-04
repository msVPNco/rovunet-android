package android.content
open class Context { val started=mutableListOf<Intent>(); fun startService(intent: Intent) { started.add(intent) } }
class Intent(context: Context, cls: Class<*>) { var action:String?=null; val extras=mutableMapOf<String,Any>(); fun putExtra(key:String,value:Any):Intent { extras[key]=value; return this } }
