package app.slipnet.domain.model

import org.json.JSONArray
import org.json.JSONObject

/** Only grouping metadata is stored. Restoring never rolls back connection settings. */
object RegroupHistory {
    fun encode(before: List<ServerProfile>, after: List<ServerProfile>): String {
        val old = before.associateBy { it.id }
        return JSONArray().apply {
            after.forEach { p -> old[p.id]?.let { b -> put(JSONObject()
                .put("id", p.id).put("oldId", b.collectionId).put("oldName", b.collectionName)
                .put("newId", p.collectionId).put("newName", p.collectionName)) } }
        }.toString()
    }
    fun restore(snapshot: String, current: List<ServerProfile>): List<ServerProfile> {
        val byId = current.associateBy { it.id }
        val records = JSONArray(snapshot)
        return (0 until records.length()).mapNotNull { i ->
            val r = records.getJSONObject(i)
            byId[r.getLong("id")]?.let { p ->
                val old = p.collectionId == r.getString("oldId") && p.collectionName == r.getString("oldName")
                val applied = p.collectionId == r.getString("newId") && p.collectionName == r.getString("newName")
                check(old || applied) { "Groups changed after regrouping; undo cancelled" }
                p.copy(collectionId = r.getString("oldId"), collectionName = r.getString("oldName"))
            }
        }
    }
}
