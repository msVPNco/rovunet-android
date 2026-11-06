package app.slipnet.core

/** Comparison for published numeric versions and alpha/beta/rc tags, not lexical ordering. */
object ReleaseVersion {
    private val format = Regex("^v?(\\d+)\\.(\\d+)(?:\\.(\\d+))?(?:-(alpha|beta|rc)(?:[.-]?(\\d+))?)?(?:-lite)?$")
    private fun parse(value: String): List<Long>? {
        val m = format.matchEntire(value) ?: return null
        val major = m.groupValues[1].toLongOrNull() ?: return null
        val minor = m.groupValues[2].toLongOrNull() ?: return null
        val patch = if (m.groupValues[3].isEmpty()) 0L else m.groupValues[3].toLongOrNull() ?: return null
        val stage = when (m.groupValues[4]) { "alpha" -> 0L; "beta" -> 1L; "rc" -> 2L; else -> 3L }
        val revision = if (m.groupValues[5].isEmpty()) 0L else m.groupValues[5].toLongOrNull() ?: return null
        return listOf(major, minor, patch, stage, revision)
    }
    fun supports(version: String): Boolean = parse(version) != null
    fun isNewer(remote: String, current: String): Boolean {
        val r = parse(remote) ?: return false
        val c = parse(current) ?: return false
        for (i in r.indices) if (r[i] != c[i]) return r[i] > c[i]
        return false
    }
}
