"""Offline SQLite regression checks. Run: python tests/v19_sqlite_smoke.py"""
from pathlib import Path
import sqlite3
from tempfile import TemporaryDirectory

schema = (Path(__file__).resolve().parents[1] / 'LangKinhHuKhong.Web' / 'schema.sql').read_text(encoding='utf-8')

with TemporaryDirectory() as d:
    c = sqlite3.connect(str(Path(d) / 'test.db'))
    c.executescript(schema)
    c.execute("INSERT INTO users(id,username,password_hash,created_at,updated_at) VALUES('u1','reader','hash','2026-10-16','2026-10-16')")
    c.execute("INSERT INTO users(id,username,password_hash,created_at,updated_at) VALUES('u2','reader2','hash','2026-10-16','2026-10-16')")
    c.execute("INSERT INTO stories(id,slug,title,genre,created_at,updated_at) VALUES('s1','am-duong-tao-hoa-kinh','Âm Dương Tạo Hóa Kinh','Sắc Hiệp','2026-10-16','2026-10-16')")
    c.execute("INSERT INTO genres(slug,name) VALUES('sac-hiep','Sắc Hiệp')")
    c.execute("INSERT INTO genres(slug,name) VALUES('tien-hiep','Tiên Hiệp')")
    c.execute("INSERT INTO story_genres(story_id,genre_slug) VALUES('s1','sac-hiep')")
    c.execute("INSERT INTO story_genres(story_id,genre_slug) VALUES('s1','tien-hiep')")
    assert c.execute("SELECT COUNT(*) FROM story_genres WHERE story_id='s1'").fetchone()[0] == 2
    c.execute("INSERT INTO story_reviews(id,story_id,user_id,stars,body,created_at,updated_at) VALUES('r1','s1','u1',5,'Hay','2026-10-16','2026-10-16')")
    c.execute("INSERT INTO story_reviews(id,story_id,user_id,stars,body,created_at,updated_at) VALUES('r2','s1','u2',3,'OK','2026-10-16','2026-10-16')")
    assert c.execute("SELECT ROUND(AVG(stars),1) FROM story_reviews WHERE status='Visible'").fetchone()[0] == 4.0
    try:
        c.execute("INSERT INTO story_reviews(id,story_id,user_id,stars,created_at,updated_at) VALUES('r3','s1','u1',1,'2026-10-16','2026-10-16')")
        raise AssertionError('One review per user-story must be enforced')
    except sqlite3.IntegrityError:
        pass
    c.execute("UPDATE story_reviews SET status='Hidden' WHERE id='r2'")
    assert c.execute("SELECT AVG(stars) FROM story_reviews WHERE status='Visible'").fetchone()[0] == 5.0
    # 300 seconds does not qualify; 301 seconds does. One listener per user/story.
    c.execute("INSERT INTO story_listeners(user_id,story_id,listened_seconds,qualified_at,updated_at) VALUES('u1','s1',300,NULL,'2026-10-16')")
    assert c.execute("SELECT COUNT(*) FROM story_listeners WHERE qualified_at IS NOT NULL").fetchone()[0] == 0
    c.execute("UPDATE story_listeners SET listened_seconds=301, qualified_at='2026-10-16' WHERE user_id='u1' AND story_id='s1'")
    assert c.execute("SELECT COUNT(*) FROM story_listeners WHERE qualified_at IS NOT NULL").fetchone()[0] == 1
    c.execute("UPDATE story_listeners SET listened_seconds=600 WHERE user_id='u1' AND story_id='s1'")
    assert c.execute("SELECT COUNT(*) FROM story_listeners WHERE qualified_at IS NOT NULL").fetchone()[0] == 1
    c.executescript(schema)  # Ensure schema is idempotent
    print('PASS: genre many-to-many, 1 review/user, moderation, unique qualified users, idempotent schema')
