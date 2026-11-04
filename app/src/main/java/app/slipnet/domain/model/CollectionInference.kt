package app.slipnet.domain.model

/** Offline UI grouping only. Never authenticates, resolves DNS, compares passwords, or mutates a connection. */
object CollectionInference {
    data class Group(val name: String, val profiles: List<ServerProfile>)
    private data class Facts(val p: ServerProfile, val index: Int, val user: String, val origin: String, val hosts: Set<String>)
    private class Bucket(val explicit: String = "", val user: String = "", val origin: String = "") {
        val entries = mutableListOf<Facts>()
        val hosts get() = entries.flatMap { it.hosts }.toSet()
    }
    private fun host(raw: String): String {
        var h = raw.trim().trimEnd('.').removePrefix("[").removeSuffix("]").lowercase()
        // Only numeric IPv6 literals reach this parser; hostnames never trigger DNS.
        if (':' in h && Regex("[0-9a-f:.]+").matches(h)) {
            h = runCatching { java.net.InetAddress.getByName(h).hostAddress.orEmpty() }.getOrDefault(h)
        }
        return h.takeIf { it.isNotEmpty() && it !in setOf("localhost", "0.0.0.0", "::", "::1") && !it.startsWith("127.") } ?: ""
    }
    private fun ip(h: String): Boolean = h.contains(':') || Regex("[0-9]+(?:\\.[0-9]+){3}").matches(h)
    private fun facts(p: ServerProfile, i: Int): Facts {
        val users = listOf(p.sshUsername, p.socksUsername.orEmpty(), p.naiveUsername).filter { it.isNotBlank() }.distinct()
        val user = when (users.size) { 0 -> p.importAccountHint; 1 -> users.single(); else -> "!conflict:$i" }
        val domain = host(p.domain)
        val backend = host(p.sshHost)
        // CDN IP, resolver IP, ports and server public keys are NOT account/server identities.
        val origin = backend.takeIf { ip(it) && it.isNotEmpty() } ?: domain.takeIf { ip(it) }.orEmpty()
        val hosts = mutableSetOf<String>()
        if (domain.isNotEmpty()) hosts += domain
        if (backend.isNotEmpty()) hosts += backend
        if (ProfileCollections.isDns(p) && domain.substringBefore('.') in setOf("t", "ts", "s", "ss", "v", "vs", "r", "rs", "dnstt", "noizdns", "slipstream", "vaydns", "soar")) {
            val parent = domain.substringAfter('.', "")
            if (parent.contains('.')) hosts += parent
        }
        return Facts(p, i, user, origin, hosts)
    }
    fun plan(profiles: List<ServerProfile>): List<Group> {
        val all = profiles.mapIndexed { i, p -> facts(p, i) }
        val buckets = mutableListOf<Bucket>()
        val assigned = mutableSetOf<Int>()
        fun add(b: Bucket, f: Facts) { b.entries += f; assigned += f.index }
        // Native metadata is authoritative within this batch, but never joins an existing database group by itself.
        all.filter { it.p.collectionId.isNotBlank() }.groupBy { it.p.collectionId }.forEach { (id, entries) ->
            val b = Bucket(id, entries.map { it.user }.filter { it.isNotEmpty() }.distinct().singleOrNull().orEmpty(), entries.map { it.origin }.filter { it.isNotEmpty() }.distinct().singleOrNull().orEmpty())
            buckets += b; entries.forEach { add(b, it) }
        }
        all.filter { it.index !in assigned && it.user.isNotEmpty() && it.origin.isNotEmpty() }.forEach { f ->
            val matches = buckets.filter { it.user == f.user && it.origin == f.origin }
            val b = matches.singleOrNull() ?: Bucket(user = f.user, origin = f.origin).also { buckets += it }
            add(b, f)
        }
        all.filter { it.index !in assigned && it.user.isNotEmpty() }.forEach { f ->
            val matches = buckets.filter { it.user == f.user && it.hosts.intersect(f.hosts).isNotEmpty() }
            val b = matches.singleOrNull() ?: Bucket(user = f.user).also { buckets += it }
            add(b, f)
        }
        // UUID-only and anonymous paths attach only if exactly one group shares server evidence.
        all.filter { it.index !in assigned }.forEach { f ->
            val matches = buckets.filter { b -> b.user.isNotEmpty() || b.explicit.isNotEmpty() }.filter { b ->
                if (f.origin.isNotEmpty() && b.origin.isNotEmpty()) f.origin == b.origin
                else b.hosts.intersect(f.hosts).isNotEmpty()
            }
            val b = matches.singleOrNull() ?: Bucket(origin = f.origin).also { buckets += it }
            add(b, f)
        }
        val groups = buckets.sortedBy { it.entries.minOf { e -> e.index } }.map { b ->
            val entries = b.entries.sortedBy { it.index }.map { it.p }
            val server = b.hosts.filter { !ip(it) }.minByOrNull { it.length } ?: b.origin
            val name = entries.firstOrNull { it.collectionName.isNotBlank() }?.collectionName
                ?: listOf(b.user.takeUnless { it.startsWith("!conflict:") }.orEmpty(), server).filter { it.isNotBlank() }.joinToString(" — ").ifBlank { entries.first().name }
            Group(name.take(120), entries)
        }
        val repeated = groups.groupingBy { it.name }.eachCount()
        val ordinal = mutableMapOf<String, Int>()
        return groups.map { group ->
            if ((repeated[group.name] ?: 0) < 2) group else {
                val n = (ordinal[group.name] ?: 0) + 1; ordinal[group.name] = n
                group.copy(name = group.name.take(110) + " ($n)")
            }
        }
    }
}
