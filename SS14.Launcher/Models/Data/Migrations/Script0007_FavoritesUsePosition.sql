ALTER TABLE FavoriteServer
ADD Position INTEGER NOT NULL DEFAULT 0;
ALTER TABLE FavoriteServer
DROP COLUMN RaiseTime;
