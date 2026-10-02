-- Skjemaet for ClaimTheSquare.
--
-- Kjøres automatisk av postgres-imaget den første gangen datavolumet er tomt
-- (alt i /docker-entrypoint-initdb.d kjøres én gang, i alfabetisk rekkefølge).
--
-- Merk: "index" MÅ stå i anførselstegn. index er et reservert ord i SQL, og
-- PostgreSQL bretter dessuten uquotede identifikatorer til lavere bokstaver.
CREATE TABLE IF NOT EXISTS text_object (
    "index"    integer PRIMARY KEY,
    text       text NOT NULL,
    back_color text NOT NULL,
    fore_color text NOT NULL
);
