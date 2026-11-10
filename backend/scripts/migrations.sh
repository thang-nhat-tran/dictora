#!/usr/bin/env bash
# EF Core migrations for one module (each module has its own DbContext and schema, see ADR-001).
#
# Usage (from anywhere):
#   backend/scripts/migrations.sh add    <Module> <MigrationName>   # e.g. add Lessons AddSegments
#   backend/scripts/migrations.sh remove <Module>                   # remove the last, unapplied migration
#   backend/scripts/migrations.sh list   <Module>
#   backend/scripts/migrations.sh update <Module>                   # apply to the DB in appsettings.Development.json
#
# Modules: Identity, Lessons, Practice
set -euo pipefail
cd "$(dirname "$0")/.."

usage() { sed -n '4,9p' "$0" | sed 's/^# \{0,1\}//'; exit 1; }

command=${1:-}; module=${2:-}
[[ -n $command && -n $module ]] || usage

case $module in
  Identity) context=IdentityModuleDbContext ;;
  Lessons)  context=LessonsDbContext ;;
  Practice) context=PracticeDbContext ;;
  *) echo "Unknown module '$module'. Expected: Identity, Lessons, Practice." >&2; exit 1 ;;
esac

common=(--context "$context" --project "src/Modules/$module/Dictora.Modules.$module" --startup-project src/Dictora.Api)

dotnet tool restore > /dev/null

case $command in
  add)
    name=${3:-}; [[ -n $name ]] || usage
    dotnet ef migrations add "$name" "${common[@]}" --output-dir Infrastructure/Migrations ;;
  remove) dotnet ef migrations remove "${common[@]}" ;;
  list)   dotnet ef migrations list "${common[@]}" ;;
  update) dotnet ef database update "${common[@]}" ;;
  *) usage ;;
esac
