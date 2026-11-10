PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS users (
 id TEXT PRIMARY KEY,
 username TEXT NOT NULL UNIQUE,
 password_hash TEXT NOT NULL,
 full_name TEXT NOT NULL DEFAULT '',
 phone TEXT NOT NULL DEFAULT '',
 address TEXT NOT NULL DEFAULT '',
 role TEXT NOT NULL DEFAULT 'User' CHECK(role IN ('User','Admin')),
 status TEXT NOT NULL DEFAULT 'Active' CHECK(status IN ('Active','Locked','Deleted')),
 created_at TEXT NOT NULL,
 updated_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS stories (
 id TEXT PRIMARY KEY,
 slug TEXT NOT NULL UNIQUE,
 title TEXT NOT NULL,
 author TEXT NOT NULL DEFAULT '',
 genre TEXT NOT NULL DEFAULT '',
 description TEXT NOT NULL DEFAULT '',
 cover_url TEXT NOT NULL DEFAULT '',
 cover_file TEXT NOT NULL DEFAULT '',
 storage_prefix TEXT NOT NULL DEFAULT '',
 drive_folder_id TEXT NOT NULL DEFAULT '',
 status TEXT NOT NULL DEFAULT 'Draft' CHECK(status IN ('Draft','Published','Hidden')),
 created_at TEXT NOT NULL,
 updated_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS episodes (
 id TEXT PRIMARY KEY,
 story_id TEXT NOT NULL REFERENCES stories(id),
 title TEXT NOT NULL,
 episode_number INTEGER NOT NULL,
 full_file TEXT NOT NULL,
 preview_file TEXT NOT NULL,
 hls_ref TEXT NOT NULL DEFAULT '',
 duration_seconds INTEGER NOT NULL DEFAULT 0,
 status TEXT NOT NULL DEFAULT 'Published' CHECK(status IN ('Published','Hidden')),
 created_at TEXT NOT NULL,
 UNIQUE(story_id, episode_number)
);
CREATE INDEX IF NOT EXISTS ix_episodes_story ON episodes(story_id,episode_number);
CREATE INDEX IF NOT EXISTS ix_episodes_story_status ON episodes(story_id,status,episode_number);
CREATE INDEX IF NOT EXISTS ix_stories_status_created ON stories(status,created_at DESC);
CREATE INDEX IF NOT EXISTS ix_stories_status_updated ON stories(status,updated_at DESC);
CREATE TABLE IF NOT EXISTS subscription_plans (
 id TEXT PRIMARY KEY,
 code TEXT NOT NULL UNIQUE,
 name TEXT NOT NULL,
 price_vnd INTEGER NOT NULL CHECK(price_vnd>=0),
 duration_months INTEGER NOT NULL CHECK(duration_months>0),
 updated_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS bank_accounts (
 id INTEGER PRIMARY KEY CHECK(id=1),
 bank_bin TEXT NOT NULL DEFAULT '',
 bank_name TEXT NOT NULL DEFAULT '',
 account_number TEXT NOT NULL DEFAULT '',
 account_name TEXT NOT NULL DEFAULT '',
 updated_at TEXT NOT NULL
);

-- V1.9.2: multiple receiving accounts; bank_accounts (id=1) stays as migration source only.
CREATE TABLE IF NOT EXISTS bank_receivers (
 id TEXT PRIMARY KEY,
 label TEXT NOT NULL DEFAULT '',
 bank_bin TEXT NOT NULL,
 bank_name TEXT NOT NULL,
 account_number TEXT NOT NULL,
 account_name TEXT NOT NULL,
 is_active INTEGER NOT NULL DEFAULT 1 CHECK(is_active IN (0,1)),
 is_default INTEGER NOT NULL DEFAULT 0 CHECK(is_default IN (0,1)),
 created_at TEXT NOT NULL,
 updated_at TEXT NOT NULL,
 UNIQUE(bank_bin,account_number)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_bank_receivers_one_default ON bank_receivers(is_default) WHERE is_default=1;
CREATE TABLE IF NOT EXISTS app_migrations (
 key TEXT PRIMARY KEY,
 applied_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS orders (
 id TEXT PRIMARY KEY,
 code TEXT NOT NULL UNIQUE,
 user_id TEXT NOT NULL REFERENCES users(id),
 plan_id TEXT NOT NULL REFERENCES subscription_plans(id),
 amount_vnd INTEGER NOT NULL,
 duration_months INTEGER NOT NULL,
 bank_bin TEXT NOT NULL,
 bank_name TEXT NOT NULL,
 account_number TEXT NOT NULL,
 account_name TEXT NOT NULL,
 status TEXT NOT NULL DEFAULT 'Pending' CHECK(status IN ('Pending','Reviewing','Paid','Rejected','Cancelled','Expired','Refunded')),
 created_at TEXT NOT NULL,
 expires_at TEXT NOT NULL,
 paid_at TEXT,
 reviewed_by TEXT,
 review_note TEXT NOT NULL DEFAULT '',
 order_type TEXT NOT NULL DEFAULT 'Purchase' CHECK(order_type IN ('Purchase','AdminGrant')),
 grant_reason TEXT NOT NULL DEFAULT '',
 premium_start_at TEXT,
 premium_end_at TEXT
);
CREATE INDEX IF NOT EXISTS ix_orders_user ON orders(user_id,created_at DESC);
CREATE INDEX IF NOT EXISTS ix_orders_status_expires ON orders(status,order_type,expires_at);
CREATE TABLE IF NOT EXISTS payments (
 id TEXT PRIMARY KEY,
 order_id TEXT NOT NULL UNIQUE REFERENCES orders(id),
 bank_reference TEXT NOT NULL UNIQUE,
 amount_vnd INTEGER NOT NULL,
 confirmed_by TEXT NOT NULL REFERENCES users(id),
 confirmed_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS subscriptions (
 id TEXT PRIMARY KEY,
 user_id TEXT NOT NULL REFERENCES users(id),
 starts_at TEXT NOT NULL,
 ends_at TEXT NOT NULL,
 source TEXT NOT NULL CHECK(source IN ('Order','Admin')),
 order_id TEXT UNIQUE REFERENCES orders(id),
 granted_by TEXT REFERENCES users(id),
 note TEXT NOT NULL DEFAULT '',
 created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_subscriptions_user ON subscriptions(user_id,ends_at DESC);
CREATE TABLE IF NOT EXISTS listening_progress (
 user_id TEXT NOT NULL REFERENCES users(id),
 episode_id TEXT NOT NULL REFERENCES episodes(id),
 seconds INTEGER NOT NULL DEFAULT 0,
 updated_at TEXT NOT NULL,
 PRIMARY KEY(user_id,episode_id)
);
CREATE INDEX IF NOT EXISTS ix_listening_user_updated ON listening_progress(user_id,updated_at DESC);
CREATE TABLE IF NOT EXISTS user_story_views (
 user_id TEXT NOT NULL REFERENCES users(id),
 story_id TEXT NOT NULL REFERENCES stories(id) ON DELETE CASCADE,
 viewed_at TEXT NOT NULL,
 PRIMARY KEY(user_id,story_id)
);
CREATE INDEX IF NOT EXISTS ix_user_story_views_user ON user_story_views(user_id,viewed_at DESC);
CREATE TABLE IF NOT EXISTS audit_logs (
 id TEXT PRIMARY KEY,
 actor_id TEXT,
 action TEXT NOT NULL,
 subject_id TEXT,
 details TEXT NOT NULL,
 created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_audit_created ON audit_logs(created_at DESC);

-- Durable deletion outbox: saved in the same SQLite transaction as any media reference change.
CREATE TABLE IF NOT EXISTS storage_cleanup (
 reference TEXT PRIMARY KEY,
 enqueued_at TEXT NOT NULL,
 attempts INTEGER NOT NULL DEFAULT 0,
 last_error TEXT NOT NULL DEFAULT ''
);

-- V1.9: independent genres, many-to-many story classification.
CREATE TABLE IF NOT EXISTS genres (
 slug TEXT PRIMARY KEY,
 name TEXT NOT NULL UNIQUE COLLATE NOCASE,
 is_active INTEGER NOT NULL DEFAULT 1 CHECK(is_active IN (0,1)),
 sort_order INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS story_genres (
 story_id TEXT NOT NULL REFERENCES stories(id) ON DELETE CASCADE,
 genre_slug TEXT NOT NULL REFERENCES genres(slug),
 PRIMARY KEY (story_id, genre_slug)
);
CREATE INDEX IF NOT EXISTS ix_story_genres_genre ON story_genres(genre_slug, story_id);

-- Exactly one review per user and story, retained as hidden when moderated.
CREATE TABLE IF NOT EXISTS story_reviews (
 id TEXT PRIMARY KEY,
 story_id TEXT NOT NULL REFERENCES stories(id) ON DELETE CASCADE,
 user_id TEXT NOT NULL REFERENCES users(id),
 stars INTEGER NOT NULL CHECK(stars BETWEEN 1 AND 5),
 body TEXT NOT NULL DEFAULT '',
 status TEXT NOT NULL DEFAULT 'Visible' CHECK(status IN ('Visible','Hidden')),
 created_at TEXT NOT NULL,
 updated_at TEXT NOT NULL,
 UNIQUE(story_id, user_id)
);
CREATE INDEX IF NOT EXISTS ix_reviews_story ON story_reviews(story_id,status,updated_at DESC);

-- Unique qualified listeners: elapsed server-side playback time, never seeking position.
CREATE TABLE IF NOT EXISTS story_listeners (
 user_id TEXT NOT NULL REFERENCES users(id),
 story_id TEXT NOT NULL REFERENCES stories(id) ON DELETE CASCADE,
 listened_seconds INTEGER NOT NULL DEFAULT 0,
 qualified_at TEXT,
 updated_at TEXT NOT NULL,
 PRIMARY KEY(user_id,story_id)
);
CREATE INDEX IF NOT EXISTS ix_story_listeners_qualified ON story_listeners(story_id,qualified_at);
CREATE TABLE IF NOT EXISTS playback_sessions (
 user_id TEXT PRIMARY KEY REFERENCES users(id),
 session_id TEXT NOT NULL,
 episode_id TEXT NOT NULL REFERENCES episodes(id) ON DELETE CASCADE,
 last_tick INTEGER NOT NULL
);

-- Story text manuscripts (.md, .txt) stored on VPS or Google Drive per story (0..N files per story).
CREATE TABLE IF NOT EXISTS story_text_files (
 id TEXT PRIMARY KEY,
 story_id TEXT NOT NULL REFERENCES stories(id) ON DELETE CASCADE,
 title TEXT NOT NULL DEFAULT '',
 file_name TEXT NOT NULL,
 stored_name TEXT NOT NULL,
 extension TEXT NOT NULL CHECK(extension IN ('.md','.txt')),
 size_bytes INTEGER NOT NULL DEFAULT 0,
 drive_file_id TEXT NOT NULL DEFAULT '',
 storage_target TEXT NOT NULL DEFAULT 'vps',
 created_at TEXT NOT NULL,
 updated_at TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS ix_story_text_files_story ON story_text_files(story_id, created_at DESC);
