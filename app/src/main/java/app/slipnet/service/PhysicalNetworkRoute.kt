package app.slipnet.service

import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities

/** Resolves the physical default, never treats our own VPN as an offline radio. No network request. */
object PhysicalNetworkRoute {
    private var previous: Network? = null

    @Synchronized
    @Suppress("DEPRECATION")
    fun current(manager: ConnectivityManager): Network? {
        fun physical(network: Network?): Boolean = network != null && manager.getNetworkCapabilities(network)?.let {
            it.hasCapability(NetworkCapabilities.NET_CAPABILITY_NOT_VPN) &&
                it.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
        } == true
        val active = manager.activeNetwork
        if (physical(active)) return active.also { previous = it }
        val caps = active?.let(manager::getNetworkCapabilities)
        // This compatibility API deliberately reports a VPN's underlying network type.
        val type = manager.activeNetworkInfo?.type
        val transport = when (type) {
            ConnectivityManager.TYPE_WIFI -> NetworkCapabilities.TRANSPORT_WIFI
            ConnectivityManager.TYPE_MOBILE -> NetworkCapabilities.TRANSPORT_CELLULAR
            ConnectivityManager.TYPE_ETHERNET -> NetworkCapabilities.TRANSPORT_ETHERNET
            else -> null
        }
        val available = manager.allNetworks.filter(::physical)
        val selected = app.slipnet.domain.model.PhysicalRouteSelection.choose(active,
            caps?.hasTransport(NetworkCapabilities.TRANSPORT_VPN) == true,
            emptyList(),
            transport, available.map { candidate -> candidate to (0..8).filter {
                manager.getNetworkCapabilities(candidate)?.hasTransport(it) == true
            }.toSet() }, previous)
        previous = selected
        return selected
    }

    fun snapshot(manager: ConnectivityManager): String? {
        val network = current(manager) ?: return null
        val links = manager.getLinkProperties(network) ?: return null
        return "$network:${links.interfaceName}:" + links.linkAddresses.map { it.toString() }.sorted().joinToString(",")
    }
}
