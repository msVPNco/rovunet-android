#!/usr/bin/env python3
"""SQLite migration check against generated Room schema; not an Android test."""
import json,pathlib,re,sqlite3
root=pathlib.Path(__file__).resolve().parents[2]
schema=json.loads((root/'app/schemas/app.slipnet.data.local.database.SlipNetDatabase/45.json').read_text())['database']
entity=next(e for e in schema['entities'] if e['tableName']=='server_profiles')
sql=entity['createSql'].replace('${TABLE_NAME}','server_profiles')
old=sql.replace("`coreLink` TEXT NOT NULL DEFAULT '', ", '')
assert old!=sql
before=sqlite3.connect(':memory:');before.execute(old)
cols=[r for r in before.execute('pragma table_info(server_profiles)') if r[1]!='id']
names=','.join('"'+r[1]+'"' for r in cols);values=[7 if r[2]=='INTEGER' else 'legacy-value' for r in cols]
for _ in range(20):before.execute(f'INSERT INTO server_profiles ({names}) VALUES ({",".join("?" for _ in cols)})',values)
rows=before.execute('select * from server_profiles').fetchall()
source=(root/'app/src/main/java/app/slipnet/data/local/database/SlipNetDatabase.kt').read_text()
migration=source.split('val MIGRATION_44_45',1)[1].split('val MIGRATION_',1)[0]
statements=re.findall(r'db.execSQL\("([^"]+)"\)',migration)
assert len(statements)==1
before.execute(statements[0])
after=sqlite3.connect(':memory:');after.execute(sql)
def columns(db):return {r[1]:r[2:] for r in db.execute('pragma table_info(server_profiles)')}
assert columns(before)==columns(after)
newrows=before.execute('select * from server_profiles').fetchall()
assert [r[:-1] for r in newrows]==rows and all(r[-1]=='' for r in newrows)
print('SQLite migration 44→45: generated schema matches and 20 legacy rows preserved')
