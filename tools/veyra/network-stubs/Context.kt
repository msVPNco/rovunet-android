package android.content
class SharedPreferences {
 private val values=java.util.concurrent.ConcurrentHashMap<String,String>()
 fun contains(key:String):Boolean=values.containsKey(key)
 fun getString(key:String,fallback:String?):String?=values[key]?:fallback
 fun edit()=Editor()
 inner class Editor { private var wipe=false; private val pending=mutableMapOf<String,String>(); private val removed=mutableSetOf<String>(); fun clear():Editor { wipe=true;return this }; fun remove(k:String):Editor { removed.add(k);return this }; fun putString(k:String,v:String):Editor { pending[k]=v;return this }; fun commit():Boolean { if(wipe)values.clear();removed.forEach{values.remove(it)};values.putAll(pending);return true } }
}
class Assets { fun open(path:String):java.io.InputStream=java.io.File("app/src/main/assets",path).inputStream() }
class Context { companion object { const val MODE_PRIVATE=0 }; val assets=Assets(); private val prefs=SharedPreferences(); fun getSharedPreferences(name:String,mode:Int)=prefs }
