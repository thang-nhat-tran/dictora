# 01: PostgreSQL schemas

> **Goal:** understand what a schema is in PostgreSQL, why Dictora gives each module its own schema, and how to work with schemas in `psql`.
> **Time:** about 30 minutes including the exercises.

---

## 1. Why it matters

Dictora has three backend modules (Identity, Lessons, Practice) that share **one** PostgreSQL database. ADR-001 says each module **owns its own data**. Schemas are how we make that ownership visible and enforceable inside a single database.

---

## 2. First, a naming trap

"Schema" means two different things in databases:

| Meaning | Example sentence | In this lesson? |
|---------|------------------|-----------------|
| **Schema = the design** of the database (tables, columns, types, relations) | "We need to change the schema to add a `duration` column." | ❌ No |
| **Schema = a named container (namespace)** inside a PostgreSQL database that holds tables | "The `segments` table lives in the `lessons` schema." | ✅ **Yes** |

This lesson is about the **second** meaning. When ADR-001 says "one schema per module", it means one *container* per module.

---

## 3. The concept

### The hierarchy

```
PostgreSQL server (one running Postgres, e.g. the Docker container)
└── Database: dictora
    ├── Schema: public          ← exists by default
    ├── Schema: identity
    │   ├── Table: users
    │   └── Table: __EFMigrationsHistory
    ├── Schema: lessons
    │   ├── Table: videos
    │   ├── Table: lessons
    │   ├── Table: segments
    │   └── Table: __EFMigrationsHistory
    └── Schema: practice
        ├── Table: attempts
        └── Table: __EFMigrationsHistory
```

An analogy: the **database** is a hard drive, a **schema** is a folder, and a **table** is a file. You can have a file called `notes.txt` in two different folders without a clash, and the same goes for a table name in two schemas.

### Full names

Every table has a full name: `schema.table`.

```sql
SELECT * FROM lessons.segments;
SELECT * FROM practice.attempts;
```

There's a `__EFMigrationsHistory` table in **each** schema, but they don't clash, because their full names are different: `lessons.__EFMigrationsHistory` vs `practice.__EFMigrationsHistory`. Lesson 02 explains what that table is.

### `public` and `search_path`

- Every new database comes with a schema called **`public`**. If you never think about schemas, all your tables go there.
- When you write a table name **without** a schema, PostgreSQL looks it up using the **`search_path`** setting, which is a list of schemas to try in order. By default it's `"$user", public`.

```sql
SHOW search_path;                 -- "$user", public
SELECT * FROM segments;           -- ❌ error: relation "segments" does not exist (it's not in public)
SELECT * FROM lessons.segments;   -- ✅ works
SET search_path = lessons, public;
SELECT * FROM segments;           -- ✅ now works for this session
```

**In Dictora we always use full names** in SQL and let EF Core handle the schema (see lesson 02). Don't rely on `search_path`.

### Schema vs separate database

Why not give each module its own database?

| | Separate schemas, one database | Separate databases |
|---|---|---|
| Hosting cost / setup | One database to run, back up and connect to | Several |
| Connection strings | One | One per module |
| Clear ownership | ✅ Yes, by schema | ✅ Yes |
| Can you accidentally join across modules? | Technically yes, so we forbid it by rule | No, Postgres can't join across databases |
| Move a module to its own service later | Easy: move one schema | Already separate |

For 2–3 devs on Railway, **one database with a schema per module** gives us clear ownership at the lowest cost. That's the choice ADR-001 made.

### What schemas do *not* do on their own

PostgreSQL **allows** queries and foreign keys across schemas:

```sql
-- Postgres accepts this. Dictora's rules forbid it.
SELECT a.*, s.text
FROM practice.attempts a
JOIN lessons.segments s ON s.id = a.segment_id;
```

So the schema boundary is a **convention we enforce**, not something the database forces on us. Our rules (in `docs/conventions/backend.md`):
- A module only reads and writes **its own schema**.
- **No foreign keys across schemas.** `practice.attempts.segment_id` is just a `uuid` column, not an FK to `lessons.segments`.
- If Practice needs segment text, it asks the Lessons module through `ILessonsApi`, never by SQL.

Why so strict? If Practice joins Lessons' tables, then Lessons can't rename a column without breaking Practice. The modules become tangled, and the main benefit of ADR-001 is gone.

### Bonus: permissions
Schemas can also have their own permissions (`GRANT USAGE ON SCHEMA lessons TO ...`). We don't use that yet, since the whole app connects as one user. It's how you'd harden the boundaries later.

---

## 4. In Dictora

| Module | Schema | Owns tables like |
|--------|--------|------------------|
| Identity | `identity` | users, roles, logins (ASP.NET Identity) |
| Lessons | `lessons` | videos, captions, lessons, segments |
| Practice | `practice` | attempts, lesson progress |

Naming: schemas and tables are **snake_case** (see `docs/conventions/backend.md`).

You'll almost never write `CREATE SCHEMA` yourself. EF Core migrations create the schema for you (lesson 02).

---

## 5. Try it (about 15 minutes)

You only need Docker. This is a throwaway database that is deleted when you're done.

```bash
# 1. Start a temporary Postgres and open psql in it
docker run --rm -d --name pg-lesson -e POSTGRES_PASSWORD=pw postgres:18
sleep 3
docker exec -it pg-lesson psql -U postgres
```

Inside `psql`:

```sql
-- 2. Look around: list schemas
\dn

-- 3. Create two module schemas
CREATE SCHEMA lessons;
CREATE SCHEMA practice;
\dn

-- 4. Same table name in two schemas: no clash
CREATE TABLE lessons.notes  (id int PRIMARY KEY, text text);
CREATE TABLE practice.notes (id int PRIMARY KEY, score int);
\dt lessons.*
\dt practice.*

-- 5. Unqualified names use search_path
INSERT INTO lessons.notes VALUES (1, 'hello');
SELECT * FROM notes;              -- error: not found in public
SET search_path = lessons;
SELECT * FROM notes;              -- finds lessons.notes
RESET search_path;

-- 6. See that Postgres ALLOWS a cross-schema join (which our rules forbid)
SELECT * FROM lessons.notes l JOIN practice.notes p ON p.id = l.id;

-- 7. Dropping a schema drops everything inside it
DROP SCHEMA practice CASCADE;
\dn

\q
```

```bash
# 8. Clean up
docker stop pg-lesson
```

---

## 6. Self-check

Try answering before you open each answer.

<details><summary>1. What's the full name of the <code>segments</code> table in Dictora?</summary>

`lessons.segments`
</details>

<details><summary>2. Can <code>identity</code> and <code>practice</code> both have a table called <code>__EFMigrationsHistory</code>?</summary>

Yes. Their full names differ (`identity.__EFMigrationsHistory` vs `practice.__EFMigrationsHistory`).
</details>

<details><summary>3. Does PostgreSQL stop the Practice module from joining <code>lessons.segments</code>?</summary>

No. Postgres allows cross-schema joins and FKs. Our **rules** forbid them, and code review plus the module structure (separate DbContexts, lesson 02) keep them out.
</details>

<details><summary>4. Practice needs the text of a segment to check an answer. How does it get it?</summary>

It calls the Lessons module's contract (e.g. `ILessonsApi.GetSegmentAsync(segmentId)`). It never queries `lessons.segments` directly.
</details>

<details><summary>5. Why one database with three schemas instead of three databases?</summary>

One thing to host, back up and connect to (cheaper and simpler on Railway), while still giving each module clear ownership of its tables.
</details>

---

## 7. Further reading
- PostgreSQL docs, **Schemas**: https://www.postgresql.org/docs/current/ddl-schemas.html
- PostgreSQL docs, **`psql` meta-commands** (`\dn`, `\dt`, …): https://www.postgresql.org/docs/current/app-psql.html

**Next:** [02: EF Core DbContext](02-ef-core-dbcontext.md)
