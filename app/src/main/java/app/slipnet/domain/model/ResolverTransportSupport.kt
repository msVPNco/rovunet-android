package app.slipnet.domain.model

/** Nullable values preserve untested paths; field names retain saved Pair JSON compatibility. */
data class ResolverTransportSupport(val first: Boolean? = null, val second: Boolean? = null)
