# Rename credentials/optl → credentials/otel

## Problem Statement

The `credentials/optl` folder is misnamed — the letters `t` and `l` in `otel` are transposed. Every reference to this folder (Docker Compose stack name, running container names, VPS workspace path, CI/CD pipeline, and documentation) inherits the typo. Anyone reading `docker ps`, server logs, or the codebase sees `optl-kibana-1`, `workspace/optl`, `job: optl` — none of which map to any recognisable technology name. The correct abbreviation for OpenTelemetry is `otel`.

## Solution

Rename the folder from `optl` to `otel` everywhere: the directory itself inside the credentials submodule, the Docker Compose stack name (which drives all container naming), the VPS workspace path, all CI/CD pipeline references, and documentation. The CI deploy step is updated to explicitly tear down and delete the old stack before the new one is brought up, ensuring no orphaned `optl-*` containers linger on the server.

## User Stories

1. As an operator, I want the observability stack folder named `otel`, so that its name matches the technology it represents (OpenTelemetry).
2. As an operator, I want running containers named `otel-*` instead of `optl-*`, so that `docker ps` and server logs are readable and unambiguous.
3. As an operator, I want the old `optl-*` containers stopped and removed automatically on deploy, so that they do not waste RAM and CPU on the server.
4. As an operator, I want `workspace/optl` deleted from the VPS during deploy, so that disk space is not wasted by the abandoned directory.
5. As an operator, I want the CI deploy to handle the full transition without manual SSH intervention, so that the rename is self-contained in one pipeline run.
6. As an operator, I want the Kibana and Fleet Server health checks to pass after the rename, so that observability is restored without manual action.
7. As a developer, I want `credentials/.github/workflows/terraform.yml` to reference `otel` throughout, so that the pipeline is consistent with the folder structure.
8. As a developer, I want the `post-upgrade.sh` script to reference `otel-fleet-server-1`, so that it targets the correct container after the rename.
9. As a developer, I want the job display name fixed from `'OpenOpenTelemetry'` to `'OpenTelemetry'`, so that the CI UI shows the correct name.
10. As a developer, I want `docs/observability.md` to reference `credentials/otel`, so that documentation matches the actual folder structure.

## Implementation Decisions

- **Folder rename**: `git mv` inside the credentials submodule renames `optl/` to `otel/`. This preserves git history for all files inside.

- **Docker Compose stack name**: The `name:` field in `docker-compose.yml` changes from `optl` to `otel`. Docker treats stacks by name, so this causes all containers to be recreated on next deploy (`otel-kibana-1`, `otel-fleet-server-1`, etc.). Brief downtime is accepted.

- **Teardown of old stack in CI**: The Terraform deploy SSH block gains a step — before rsync — that tears down the old stack and deletes its directory. A guard makes this idempotent — safe to run on subsequent deploys when `optl` is already gone.

- **Container name references**: `post-upgrade.sh` hardcodes `optl-fleet-server-1` — updated to `otel-fleet-server-1`.

- **Typo fix**: `'OpenOpenTelemetry'` → `'OpenTelemetry'` in `terraform.yml` job display name, done in the same commit.

- **Two-commit strategy**: All credentials submodule changes (folder rename + file edits) land in one submodule commit. The main repo then gets a separate commit that bumps the submodule pointer and updates `docs/observability.md`.

- **`otlp` references are not touched**: Values like `OTEL_TRACES_EXPORTER: 'otlp'` refer to the OpenTelemetry Protocol wire format — a different acronym — and are correct as-is.

## Testing Decisions

A good test verifies only external, observable behaviour: the old stack is gone, the new stack is healthy, and no orphaned resources remain.

The natural seam is the CI pipeline itself:
- The existing health-check loops in `terraform.yml` (polling `docker inspect otel-kibana-1` and `docker inspect otel-fleet-server-1` for `healthy`) serve as the integration gate — the pipeline fails if the containers do not come up.
- After a successful run: `docker ps` on the VPS shows `otel-*` containers only; `workspace/optl` is absent; `docs/observability.md` paths resolve correctly.

No unit tests are needed — this is a rename with no logic changes.

## Out of Scope

- Renaming `otlp` protocol values (`OTEL_TRACES_EXPORTER: 'otlp'`, etc.) — these are the correct OpenTelemetry Protocol acronym, not the folder name.
- Elasticsearch data migration — named volumes survive container recreation.
- Changes to any service outside the credentials submodule and `docs/observability.md`.
- Running down-migrations or archiving old Elasticsearch indices.

## Further Notes

The credentials submodule (`credentials/.git`) must be committed independently before the main repo commit that bumps its pointer. If the submodule commit is missing, the main repo will reference a non-existent commit.

The teardown step uses `~/workspace/optl` (absolute home-relative path) rather than a relative path to avoid ambiguity regardless of the SSH working directory.

The `docker image prune -a -f` step that already exists in `pipeline.yml` will clean up any unused images left over from the old `optl` containers after the new stack is running.
