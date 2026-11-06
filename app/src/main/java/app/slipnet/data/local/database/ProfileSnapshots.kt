package app.slipnet.data.local.database

import android.util.Base64
import com.google.gson.Gson
import java.security.MessageDigest

/** Includes every stored column, including ciphertext. This is an in-memory review token, not a database revision. */
internal object ProfileSnapshots {
    private val gson = Gson()
    fun digest(row: ProfileEntity): String = Base64.encodeToString(
        MessageDigest.getInstance("SHA-256").digest(gson.toJson(row).toByteArray(Charsets.UTF_8)), Base64.NO_WRAP)
}

data class ReviewedProfileRow(val id: Long, val digest: String)
