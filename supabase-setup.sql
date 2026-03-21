-- ============================================================
-- RaveRadar — Supabase PostgreSQL Setup
-- Run this entire script in Supabase → SQL Editor
-- ============================================================

-- EF Core migration history table (tells the backend migrations are already applied)
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" varchar(150) NOT NULL,
    "ProductVersion" varchar(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

-- ============================================================
-- SCHEMA
-- ============================================================

CREATE TABLE IF NOT EXISTS "Artists" (
    "Id"         SERIAL PRIMARY KEY,
    "Name"       text NOT NULL,
    "SpotifyId"  text,
    "ImageUrl"   text,
    "Genres"     text NOT NULL DEFAULT '',
    "Popularity" integer NOT NULL DEFAULT 0,
    "Bio"        text,
    "TopTracks"  text NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS "Genres" (
    "Id"   SERIAL PRIMARY KEY,
    "Name" text NOT NULL
);

CREATE TABLE IF NOT EXISTS "Events" (
    "Id"          text NOT NULL,
    "Name"        text NOT NULL,
    "Date"        timestamptz NOT NULL,
    "Venue"       text,
    "City"        text,
    "TicketUrl"   text,
    "ImageUrl"    text,
    "Latitude"    double precision NOT NULL DEFAULT 0,
    "Longitude"   double precision NOT NULL DEFAULT 0,
    "ArtistNames" text NOT NULL DEFAULT '',
    "GenreNames"  text NOT NULL DEFAULT '',
    "Source"      text,
    "SourceId"    text,
    CONSTRAINT "PK_Events" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS "Users" (
    "Id"            SERIAL PRIMARY KEY,
    "Email"         text NOT NULL,
    "PasswordHash"  text NOT NULL,
    "Location"      text,
    "FavoriteSongs" text NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS "SavedTracks" (
    "Id"              SERIAL,
    "SpotifyTrackId"  text,
    "SongName"        text NOT NULL,
    "ArtistName"      text NOT NULL,
    "ArtistSpotifyId" text,
    "ImageUrl"        text,
    "PreviewUrl"      text,
    "ExternalUrl"     text,
    "Genres"          text NOT NULL DEFAULT '',
    "Vibes"           text NOT NULL DEFAULT '',
    "AddedAt"         timestamptz NOT NULL DEFAULT now(),
    "UserId"          integer NOT NULL,
    CONSTRAINT "PK_SavedTracks" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SavedTracks_Users_UserId" FOREIGN KEY ("UserId")
        REFERENCES "Users"("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "ArtistUser" (
    "FavoriteArtistsId" integer NOT NULL,
    "UserId"            integer NOT NULL,
    CONSTRAINT "PK_ArtistUser" PRIMARY KEY ("FavoriteArtistsId", "UserId"),
    CONSTRAINT "FK_ArtistUser_Artists" FOREIGN KEY ("FavoriteArtistsId")
        REFERENCES "Artists"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ArtistUser_Users" FOREIGN KEY ("UserId")
        REFERENCES "Users"("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "GenreUser" (
    "FavoriteGenresId" integer NOT NULL,
    "UserId"           integer NOT NULL,
    CONSTRAINT "PK_GenreUser" PRIMARY KEY ("FavoriteGenresId", "UserId"),
    CONSTRAINT "FK_GenreUser_Genres" FOREIGN KEY ("FavoriteGenresId")
        REFERENCES "Genres"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_GenreUser_Users" FOREIGN KEY ("UserId")
        REFERENCES "Users"("Id") ON DELETE CASCADE
);

-- Indexes
CREATE INDEX IF NOT EXISTS "IX_ArtistUser_UserId"    ON "ArtistUser"  ("UserId");
CREATE INDEX IF NOT EXISTS "IX_GenreUser_UserId"     ON "GenreUser"   ("UserId");
CREATE INDEX IF NOT EXISTS "IX_SavedTracks_UserId"   ON "SavedTracks" ("UserId");

-- ============================================================
-- Mark all EF Core migrations as already applied
-- (prevents the backend from trying to run SQLite-style migrations on PostgreSQL)
-- ============================================================
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES
    ('20260305035525_InitialCreate',              '8.0.13'),
    ('20260305052731_AddUserAndPreferences',      '8.0.13'),
    ('20260305052914_UpdateEventsWithMetadata',   '8.0.13'),
    ('20260306123219_UpdateArtistModel',          '8.0.13'),
    ('20260307120000_ModernizeModels',            '8.0.13'),
    ('20260308000000_AddSavedTracks',             '8.0.13'),
    ('20260321145737_AddAudioFeaturesToSavedTrack', '8.0.13')
ON CONFLICT ("MigrationId") DO NOTHING;

-- Audio feature columns added by AddAudioFeaturesToSavedTrack migration
ALTER TABLE "SavedTracks" ADD COLUMN IF NOT EXISTS "BpmValue"            real;
ALTER TABLE "SavedTracks" ADD COLUMN IF NOT EXISTS "EnergyScore"         real;
ALTER TABLE "SavedTracks" ADD COLUMN IF NOT EXISTS "DanceabilityScore"   real;
ALTER TABLE "SavedTracks" ADD COLUMN IF NOT EXISTS "ValenceScore"        real;
ALTER TABLE "SavedTracks" ADD COLUMN IF NOT EXISTS "DarknessScore"       real;
ALTER TABLE "SavedTracks" ADD COLUMN IF NOT EXISTS "AudioFeaturesEnriched" boolean NOT NULL DEFAULT false;

-- ============================================================
-- SEED: Genres
-- (The app will skip seeding if these already exist)
-- ============================================================
INSERT INTO "Genres" ("Name")
SELECT v."Name"
FROM (VALUES
    ('House'),
    ('Techno'),
    ('Dubstep'),
    ('Trance'),
    ('Drum & Bass'),
    ('Trap'),
    ('Future Bass')
) AS v("Name")
WHERE NOT EXISTS (
    SELECT 1 FROM "Genres" WHERE "Genres"."Name" = v."Name"
);

-- ============================================================
-- SEED: Featured Artists
-- (The app's DbSeeder will load 500+ more from edm_artists_dataset.txt on first start)
-- ============================================================
INSERT INTO "Artists" ("Name", "ImageUrl", "Genres", "Popularity", "Bio", "TopTracks")
SELECT v."Name", v."ImageUrl", v."Genres", v."Popularity", v."Bio", v."TopTracks"
FROM (VALUES
    ('Levity',      'https://i.scdn.co/image/ab6761610000e5eb66b5c3e7b1a0e1c9e8e60473', 'Dubstep,Electronic', 88, 'Levity is a rising electronic trio known for their high-energy performances and unique sound that blends various bass music subgenres.', 'Flip It|The Wheel|Bad Habits'),
    ('John Summit', 'https://i.scdn.co/image/ab6761610000e5eb9816174a6f790c563d417934', 'Tech House',         93, 'John Summit is a Chicago-based DJ and producer who has quickly become one of the biggest names in tech house.',                         'Where You Are|Human|La Danza'),
    ('Fred again..','https://i.scdn.co/image/ab6761610000e5eb07f0f6706e90264102604928', 'Electronic,House',   95, 'Fred again.. is a British record producer, singer, songwriter, multi-instrumentalist and DJ.',                                        'Delilah (pull me out of this)|Marea (weve lost dancing)|Jungle'),
    ('Dom Dolla',   'https://i.scdn.co/image/ab6761610000e5eb74f76263722971295f7ec91e', 'House,Tech House',   91, 'Dom Dolla is an Australian house music producer known for his signature basslines and high-energy sets.',                           'Saving Up|Rhyme Dust|San Frandisco'),
    ('Sara Landry', 'https://i.scdn.co/image/ab6761610000e5eb817441551066927909a80536', 'Hard Techno',        90, 'Sara Landry is a producer and DJ known for her dark, feminine, and industrial hard techno sound.',                                  'Legacy|Peer Pressure|Queen of the Banshees'),
    ('Knock2',      'https://i.scdn.co/image/ab6761610000e5eb73004467c699949987f6312a', 'Trap,Bass House',    90, 'Knock2 is an American DJ and producer leading the new wave of high-energy bass house and trap.',                                   'dashstar*|Rock Ur World|Make U SWEAT!')
) AS v("Name", "ImageUrl", "Genres", "Popularity", "Bio", "TopTracks")
WHERE NOT EXISTS (
    SELECT 1 FROM "Artists" WHERE "Artists"."Name" = v."Name"
);

-- ============================================================
-- SEED: Sample Events
-- (Real events will be populated by EdmTrainSyncJob after first start)
-- ============================================================
INSERT INTO "Events" ("Id", "Name", "Date", "Venue", "City", "Latitude", "Longitude", "TicketUrl", "ImageUrl", "ArtistNames", "GenreNames")
VALUES
    ('1', 'Ultra Music Festival', NOW() + INTERVAL '30 days', 'Bayfront Park',          'Miami',     25.78,  -80.18,  'https://ultramusicfestival.com',               'https://images.unsplash.com/photo-1533174072545-e8d4aa97edf9?auto=format&fit=crop&q=80&w=1000', 'Martin Garrix,Tiësto',    'House,Trance'),
    ('2', 'EDC Las Vegas',        NOW() + INTERVAL '60 days', 'Las Vegas Motor Speedway','Las Vegas', 36.27, -115.01,  'https://lasvegas.electricdaisycarnival.com/',  'https://images.unsplash.com/photo-1470225620780-dba8ba36b745?auto=format&fit=crop&q=80&w=1000', 'Excision,Illenium',       'Dubstep,Future Bass')
ON CONFLICT ("Id") DO NOTHING;
