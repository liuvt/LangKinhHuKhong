"""Offline checks for receiving-bank migration and immutable order snapshots.
Run: python tests/v192_bank_sqlite_smoke.py
Does not replace dotnet build or end-to-end banking tests.
"""
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory

base=Path(__file__).resolve().parents[1]/'LangKinhHuKhong.Web'
schema=(base/'schema.sql').read_text(encoding='utf8')
with TemporaryDirectory() as d:
    con=sqlite3.connect(str(Path(d)/'check.sqlite'))
    con.execute('PRAGMA foreign_keys=ON')
    con.executescript(schema)
    con.execute("INSERT INTO bank_accounts(id,bank_bin,bank_name,account_number,account_name,updated_at) VALUES(1,'970422','MB Bank','123456789','NGUYEN VAN A','2026-10-16')")
    # Equivalent to the one-time migration statements in Database.InitializeAsync.
    migration="""INSERT INTO bank_receivers(id,label,bank_bin,bank_name,account_number,account_name,is_active,is_default,created_at,updated_at)
        SELECT 'old','Tài khoản nhận tiền',bank_bin,bank_name,account_number,account_name,1,1,'2026-10-16','2026-10-16'
        FROM bank_accounts WHERE id=1 AND bank_bin<>'' AND account_number<>'' AND account_name<>''
        AND NOT EXISTS(SELECT 1 FROM bank_receivers)"""
    con.execute(migration)
    con.execute("INSERT INTO app_migrations(key,applied_at) VALUES('receivers-v1.9.2','2026-10-16')")
    con.execute("ALTER TABLE orders ADD COLUMN bank_receiver_id TEXT NOT NULL DEFAULT ''")
    assert con.execute('SELECT count(*) FROM bank_receivers WHERE is_default=1 AND is_active=1').fetchone()[0]==1
    con.execute("INSERT INTO bank_receivers VALUES('new','Vietcombank','970436','VCB','987654321','NGUYEN VAN B',1,0,'2026-10-16','2026-10-16')")
    # Before changing bank, the order saves its receiving account snapshot.
    con.execute("INSERT INTO users(id,username,password_hash,created_at,updated_at) VALUES('user','reader','hash','2026-10-16','2026-10-16')")
    con.execute("INSERT INTO subscription_plans(id,code,name,price_vnd,duration_months,updated_at) VALUES('premium','PREMIUM','Premium',100000,1,'2026-10-16')")
    con.execute("""INSERT INTO orders(id,code,user_id,plan_id,amount_vnd,duration_months,bank_bin,bank_name,account_number,account_name,status,created_at,expires_at,bank_receiver_id)
      VALUES('order1','LKH0001','user','premium',100000,1,'970422','MB Bank','123456789','NGUYEN VAN A','Pending','2026-10-16','2026-10-17','old')""")
    con.execute("UPDATE bank_receivers SET is_default=0 WHERE id='old'")
    con.execute("UPDATE bank_receivers SET is_default=1 WHERE id='new'")
    assert con.execute("SELECT account_number FROM orders WHERE id='order1'").fetchone()[0]=='123456789'
    assert con.execute("SELECT account_number FROM bank_receivers WHERE is_default=1").fetchone()[0]=='987654321'
    try:
        con.execute("UPDATE bank_receivers SET is_default=1 WHERE id='old'")
        raise AssertionError('Expected one-default restriction')
    except sqlite3.IntegrityError: pass
    try:
        con.execute("INSERT INTO bank_receivers VALUES('duplicate','Dup','970422','MB','123456789','NGUYEN VAN A',1,0,'2026-10-16','2026-10-16')")
        raise AssertionError('Expected account duplicate protection')
    except sqlite3.IntegrityError: pass
    con.execute("UPDATE bank_receivers SET is_active=0 WHERE id='old'")
    con.executescript(schema)
    assert con.execute("SELECT COUNT(*) FROM bank_receivers").fetchone()[0]==2
    assert con.execute("SELECT is_active FROM bank_receivers WHERE id='old'").fetchone()[0]==0
print('PASS: migration, one active default, duplicate detection, immutable orders, schema idempotency')
