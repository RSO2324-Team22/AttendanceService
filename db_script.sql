CREATE TABLE "Concerts" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "Name" text NOT NULL,
    CONSTRAINT "PK_Concerts" PRIMARY KEY ("Id")
);


CREATE TABLE "Member" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "Name" text NOT NULL,
    "Section" integer NOT NULL,
    CONSTRAINT "PK_Member" PRIMARY KEY ("Id")
);


CREATE TABLE "Rehearsals" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "Name" text NOT NULL,
    "Type" integer NOT NULL,
    CONSTRAINT "PK_Rehearsals" PRIMARY KEY ("Id")
);


CREATE TABLE "ConcertAttendances" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "MemberId" integer NOT NULL,
    "ConcertId" integer NOT NULL,
    "IsPresent" boolean NOT NULL,
    "ReasonForAbsence" text NULL,
    CONSTRAINT "PK_ConcertAttendances" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ConcertAttendances_Concerts_ConcertId" FOREIGN KEY ("ConcertId") REFERENCES "Concerts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ConcertAttendances_Member_MemberId" FOREIGN KEY ("MemberId") REFERENCES "Member" ("Id") ON DELETE CASCADE
);


CREATE TABLE "RehearsalAttendances" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY,
    "MemberId" integer NOT NULL,
    "RehearsalId" integer NOT NULL,
    "IsPresent" boolean NOT NULL,
    "ReasonForAbsence" text NULL,
    CONSTRAINT "PK_RehearsalAttendances" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_RehearsalAttendances_Member_MemberId" FOREIGN KEY ("MemberId") REFERENCES "Member" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_RehearsalAttendances_Rehearsals_RehearsalId" FOREIGN KEY ("RehearsalId") REFERENCES "Rehearsals" ("Id") ON DELETE CASCADE
);


CREATE INDEX "IX_ConcertAttendances_ConcertId" ON "ConcertAttendances" ("ConcertId");


CREATE INDEX "IX_ConcertAttendances_MemberId" ON "ConcertAttendances" ("MemberId");


CREATE INDEX "IX_RehearsalAttendances_MemberId" ON "RehearsalAttendances" ("MemberId");


CREATE INDEX "IX_RehearsalAttendances_RehearsalId" ON "RehearsalAttendances" ("RehearsalId");


