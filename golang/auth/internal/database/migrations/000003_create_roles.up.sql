CREATE TABLE IF NOT EXISTS "Roles" (
    "Id"        varchar(20) PRIMARY KEY,
    "Name"      varchar(50) NOT NULL,
    "CreatedAt" timestamptz NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS "UserRoles" (
    "UserId"    text        NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "RoleId"    varchar(20) NOT NULL REFERENCES "Roles"("Id") ON DELETE RESTRICT,
    "CreatedAt" timestamptz NOT NULL DEFAULT NOW(),
    PRIMARY KEY ("UserId", "RoleId")
);

INSERT INTO "Roles" ("Id", "Name") VALUES ('admin', 'Admin'), ('user', 'User');
