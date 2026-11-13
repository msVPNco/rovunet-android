package app.slipnet.presentation

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.net.VpnService
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.core.content.ContextCompat
import androidx.navigation.compose.rememberNavController
import app.slipnet.data.local.datastore.DarkMode
import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.presentation.navigation.NavGraph
import app.slipnet.presentation.theme.SlipstreamTheme
import app.slipnet.service.VpnConnectionManager
import dagger.hilt.android.AndroidEntryPoint
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import javax.inject.Inject

@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    @Inject
    lateinit var preferencesDataStore: PreferencesDataStore

    @Inject
    lateinit var connectionManager: VpnConnectionManager

    private val _deepLinkUri = MutableStateFlow<String?>(null)
    val deepLinkUri: StateFlow<String?> = _deepLinkUri.asStateFlow()

    fun consumeDeepLink() {
        _deepLinkUri.value = null
    }

    private var pendingVpnAction: (() -> Unit)? = null

    private val vpnPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { result ->
        if (result.resultCode == RESULT_OK) {
            pendingVpnAction?.invoke()
        }
        pendingVpnAction = null
    }

    private val notificationPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { isGranted ->
        // Notification permission result - continue regardless
    }

    override fun attachBaseContext(newBase: android.content.Context) {
        val lang = newBase.getSharedPreferences("rovunet_ui", MODE_PRIVATE).getString("language", null)
        if (lang !in setOf("en", "fa", "zh", "ru")) { super.attachBaseContext(newBase); return }
        val locale = java.util.Locale.forLanguageTag(if (lang == "zh") "zh-Hans" else lang)
        val configuration = android.content.res.Configuration(newBase.resources.configuration)
        configuration.setLocale(locale)
        configuration.setLayoutDirection(locale)
        super.attachBaseContext(newBase.createConfigurationContext(configuration))
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        enableEdgeToEdge()
        super.onCreate(savedInstanceState)

        handleDeepLink(intent)

        setContent {
            val darkMode by preferencesDataStore.darkMode.collectAsState(initial = DarkMode.SYSTEM)
            val firstLaunchDone by preferencesDataStore.firstLaunchDone.collectAsState(initial = null)
            val ready = languageReady(getSharedPreferences("rovunet_ui", MODE_PRIVATE).getString("language", null), firstLaunchDone)
            androidx.compose.runtime.LaunchedEffect(ready) { if (ready) requestNotificationPermissionIfNeeded() }

            app.slipnet.presentation.theme.RovunetLocale {
                SlipstreamTheme(darkMode = darkMode) {
                    Surface(
                        modifier = Modifier.fillMaxSize(),
                        color = MaterialTheme.colorScheme.background
                    ) {
                        when {
                            ready -> { val navController = rememberNavController(); NavGraph(navController = navController) }
                            firstLaunchDone == null -> app.slipnet.presentation.main.StartupCover()
                            firstLaunchDone == false -> LanguageWelcome { code ->
                                getSharedPreferences("rovunet_ui", MODE_PRIVATE).edit().putString("language", code).apply()
                                recreate()
                            }
                        }
                    }
                }
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        handleDeepLink(intent)
    }

    private fun handleDeepLink(intent: Intent?) {
        val uri = intent?.data?.toString()
        if (uri != null && (app.slipnet.data.export.ConfigSchemes.accepts(uri))) {
            _deepLinkUri.value = uri
        }
    }

    private fun requestNotificationPermissionIfNeeded() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(
                    this,
                    Manifest.permission.POST_NOTIFICATIONS
                ) != PackageManager.PERMISSION_GRANTED
            ) {
                notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
            }
        }
    }

    fun requestVpnPermissionAndConnect(onPermissionGranted: () -> Unit) {
        val vpnIntent = VpnService.prepare(this)
        if (vpnIntent != null) {
            pendingVpnAction = onPermissionGranted
            vpnPermissionLauncher.launch(vpnIntent)
        } else {
            onPermissionGranted()
        }
    }

    fun hasVpnPermission(): Boolean {
        return VpnService.prepare(this) == null
    }
}
