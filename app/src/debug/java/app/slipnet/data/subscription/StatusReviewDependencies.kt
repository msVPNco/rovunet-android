package app.slipnet.data.subscription

import app.slipnet.data.local.datastore.PreferencesDataStore
import app.slipnet.service.VpnConnectionManager
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent

/** App-owned Debug entry point; androidTest-only interfaces are not in this component. */
@EntryPoint
@InstallIn(SingletonComponent::class)
internal interface StatusReviewDependencies {
    fun connection(): VpnConnectionManager
    fun preferences(): PreferencesDataStore
}
