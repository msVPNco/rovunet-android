@file:OptIn(kotlinx.coroutines.InternalCoroutinesApi::class)
package audit
import kotlin.coroutines.CoroutineContext
import kotlinx.coroutines.*
import kotlinx.coroutines.internal.MainDispatcherFactory
class AuditMainDispatcher: MainCoroutineDispatcher() {
 override val immediate:MainCoroutineDispatcher get()=this
 override fun isDispatchNeeded(context:CoroutineContext)=false
 override fun dispatch(context:CoroutineContext,block:Runnable)=block.run()
}
class AuditDispatcherFactory:MainDispatcherFactory {
 override val loadPriority:Int=1000
 override fun createDispatcher(allFactories:List<MainDispatcherFactory>)=AuditMainDispatcher()
 override fun hintOnError()="audit"
}
