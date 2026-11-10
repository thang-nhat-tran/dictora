# 03: Package and tool versions

> **Goal:** understand the two files that pin versions in the backend, `Directory.Packages.props` (NuGet packages) and `dotnet-tools.json` (command-line tools), and know how to add or upgrade something.
> **Before this:** nothing. It helps to have run `dotnet build` once.
> **Time:** about 15 minutes.

---

## 1. Why it matters

The backend has about 10 projects (the API, 3 modules, 3 contracts, Shared, 2 test projects) and 2–3 developers plus CI building them. If each project or each machine picks its own versions, you get bugs that only happen for one person:
- two projects in the same process using different versions of one package;
- a migration that looks different depending on who generated it;
- "works on my machine" because CI or a new dev has a different tool installed.

These two files make sure everyone and everything uses exactly the same versions.

| | `Directory.Packages.props` | `dotnet-tools.json` |
|---|---|---|
| Controls | **Libraries** inside the app (NuGet packages) | **Command-line tools** you run (`dotnet ef`) |
| Used by | `dotnet build` / `dotnet restore`, automatically | `dotnet tool restore`, then `dotnet ef ...` |
| Goal | Every project uses the same package versions | Every dev and CI use the same tool version |
| You edit it when | Adding or upgrading a package | Adding or upgrading a tool |

---

## 2. Central package management (`Directory.Packages.props`)

### The problem
Without it, each project writes the version itself:

```xml
<!-- Lessons.csproj -->
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
<!-- Practice.csproj -->
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.1" />  <!-- oops -->
```

Someone upgrades one project and forgets the others, so versions drift apart. Since everything runs in one process, that causes warnings or confusing runtime errors. Upgrading also means editing many files.

### How it works
One file at `backend/Directory.Packages.props` holds every version:

```xml
<PropertyGroup>
  <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>   <!-- turns the feature on -->
</PropertyGroup>
<ItemGroup>
  <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
</ItemGroup>
```

- **`PackageVersion` only sets the version.** It means "if a project uses this package, use this version". It does **not** add the package to any project.
- **Each project still chooses its packages**, without a version:
  ```xml
  <!-- Dictora.Api.csproj -->
  <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
  ```
- **No import needed.** When you build, MSBuild searches upward from each project's folder for a file named `Directory.Packages.props` and applies it automatically. `Directory.Build.props` next to it works the same way and holds the shared settings (`net10.0`, Nullable, ImplicitUsings).
- **It enforces itself.** A `Version="..."` on a `PackageReference` in a csproj is a build error (NU1008).

### How you use it
- **Add a package to a project:**
  ```bash
  cd backend
  dotnet add src/Modules/Lessons/Dictora.Modules.Lessons package Some.Package
  ```
  The command writes the version into `Directory.Packages.props` and a version-less reference into the csproj. If you edit the files by hand, do the same split.
- **Upgrade a package everywhere:** change its one line in `Directory.Packages.props`.

---

## 3. Repo-local tools (`dotnet-tools.json`)

### The problem
`dotnet ef` (creates and applies migrations, see [02](02-ef-core-dbcontext.md)) is not a library in the app. It's a separate program. The common way to get it is a global install:

```bash
dotnet tool install --global dotnet-ef
```

Then every machine has whatever version it happened to install, CI has none, and a new dev has to know to install it. A different `dotnet-ef` version can generate slightly different migration files or fail against EF Core 10.

### How it works
`backend/dotnet-tools.json` (the **tool manifest**) lists the tools this repo needs, with exact versions:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-ef": { "version": "10.0.14", "commands": ["dotnet-ef"], "rollForward": false }
  }
}
```

- **`isRoot: true`:** stop here, don't look for manifests in parent folders.
- **`rollForward: false`:** use exactly 10.0.14, never a newer version.
- When you run `dotnet ef ...` anywhere inside `backend/`, the .NET CLI searches upward, finds this manifest, and runs that version. Other projects on your machine can use other versions without conflict.

### How you use it
```bash
cd backend
dotnet tool restore     # once after cloning, and again whenever the manifest changes
dotnet ef --version     # → 10.0.14
```
- `dotnet tool restore` downloads the listed tools into your NuGet cache. It does nothing if they're already there.
- You rarely run `dotnet ef` yourself: `scripts/migrations.sh` runs `dotnet tool restore` for you, then calls `dotnet ef` with the right `--project` and `--startup-project`.
- **Add another tool:** `dotnet tool install <name>` (without `--global`) adds it to the manifest. Commit the change; everyone gets it on their next restore.

---

## 4. In Dictora

- **The Design package and the tool are a pair.** `dotnet-ef` (the tool, in `dotnet-tools.json`) loads `Microsoft.EntityFrameworkCore.Design` (a package, in `Directory.Packages.props`, referenced by `Dictora.Api`) to read the DbContexts. Both are pinned to **10.0.14**. When you upgrade EF Core, change both in the same PR.
- **Design is a build-time-only package.** In `Dictora.Api.csproj` it has `<PrivateAssets>all</PrivateAssets>`, so it isn't passed on to projects that reference the API (like the test projects).
- **Packages are grouped** in `Directory.Packages.props` (`ASP.NET Core`, `Database`, `Testing`). Add new ones to the right group.

---

## 5. Try it

1. In `backend/`, run `dotnet tool restore` and then `dotnet ef --version`. Check that it prints 10.0.14.
2. Add `Version="10.0.3"` to the Npgsql `PackageReference` in `src/Shared/Dictora.Shared/Dictora.Shared.csproj` and run `dotnet build`. Read the NU1008 error, then undo the change.
3. Find which projects use `Testcontainers.PostgreSql`, which is versioned in `Directory.Packages.props` (from the repo root: `grep -r Testcontainers --include='*.csproj' backend/src backend/tests`).

---

## 6. Self-check

1. You add `Some.Package` to `Directory.Packages.props` only. Does any project now use it? *(No. A project must also have a `PackageReference`.)*
2. Where do you change the version of Npgsql for the whole backend? *(One line in `backend/Directory.Packages.props`.)*
3. A new dev clones the repo and `dotnet ef` says "command not found". What should they run? *(`dotnet tool restore` in `backend/`.)*
4. You upgrade EF Core to 10.0.20. Which two places change? *(`Microsoft.EntityFrameworkCore.Design` in `Directory.Packages.props` and `dotnet-ef` in `dotnet-tools.json`, plus the other EF Core packages if they have matching releases.)*

---

## 7. Further reading
- [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management)
- [.NET local tools](https://learn.microsoft.com/dotnet/core/tools/local-tools-how-to-use)
- [Customize your build (Directory.Build.props)](https://learn.microsoft.com/visualstudio/msbuild/customize-by-directory)
