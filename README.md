# Docker for .NET Developers — multi-stage builds, distroless images, non-root containers & layer caching

[![Build](https://github.com/varunkonnojutech-lab/docker-dotnet-mastery/actions/workflows/ci.yml/badge.svg)](https://github.com/varunkonnojutech-lab/docker-dotnet-mastery/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/varunkonnojutech-lab/docker-dotnet-mastery)

Most .NET Dockerfiles you find online work — and quietly ship the whole SDK to production, run as root, and rebuild from scratch on every code change. This repo shows the production version of the same app three ways, side by side, so you can see exactly what each practice buys you: a small API, three Dockerfiles, and a size report that makes the lesson impossible to unsee.

## Who is this for

.NET developers who can `docker run` an image but have never thought hard about what is *in* the image, or who copy-pasted a Dockerfile once and never revisited it. If you deploy to Azure Container Apps, AKS, or any registry, this is your Dockerfile education.

## What you'll learn

- **Multi-stage builds** — compile in the SDK image, ship only the runtime image, and why the SDK must never reach production
- **Layer caching** — copy the `.csproj` and restore *before* copying source, so a code change never redoes dependency restore
- **Non-root containers** — the `app` user built into .NET 8 runtime images, and why root-in-container is a real risk, not a style point
- **Distroless (chiseled) images** — what you gain (tiny image, no shell, no package manager) and what you give up (debugging inside the container)
- **`.dockerignore` discipline** — how `bin/`, `obj/`, `.git/`, and `.env` files inflate or endanger your image
- **Image size as a habit** — measuring before and after, not guessing

## Quick start

No Docker needed to run the app itself:

```bash
dotnet run --project src/DockerDemo.Api
curl localhost:5000/health      # {"status":"ok"}
curl localhost:5000/products    # the seeded catalog
```

With Docker (the point of the repo):

```bash
docker compose up --build
curl localhost:8080/health
curl localhost:8080/products
```

Run the tests:

```bash
dotnet test
```

Build all three images and compare sizes:

```bash
bash scripts/build-all.sh
```

## The app

A deliberately tiny Products API — `GET /products`, `GET /products/{id}`, `POST /products`, `DELETE /products/{id}`, plus `GET /health`. The domain is boring on purpose: every interesting decision in this repo lives in the Dockerfiles, and the API just needs to be real enough to smoke-test inside a container.

## The three Dockerfiles, compared

| | `Dockerfile.naive` | `Dockerfile` (production) | `Dockerfile.distroless` |
|---|---|---|---|
| Final base image | SDK (`dotnet/sdk:8.0`) | Runtime (`dotnet/aspnet:8.0`) | Chiseled runtime (`dotnet/aspnet:8.0-noble-chiseled`) |
| Stages | 1 | 2 (build → final) | 2 (build → final) |
| Runs as | root | `app` (non-root) | `app` (non-root) |
| Shell in final image | yes | yes | **no** |
| Layer-cached restore | no (`COPY . .` first) | yes (`.csproj` first) | yes (`.csproj` first) |
| Relative size | largest (SDK + source + build outputs) | roughly 4–5x smaller than naive | smallest of the three |
| Use it for | learning what *not* to do | most production services | security-sensitive / regulated workloads |

Sizes vary by .NET patch version and base-image updates — run `scripts/build-all.sh` and read your own numbers rather than trusting anyone's table, including this one.

## Layer caching, the one idea that pays for the whole repo

Docker caches each Dockerfile step. A step is reused only if nothing it depends on changed. So the order of your steps *is* your build performance:

```
COPY src/DockerDemo.Api/DockerDemo.Api.csproj ...   # changes only when dependencies change
RUN dotnet restore ...                              # cached across every code-only change
COPY . .                                            # changes on every code edit — fine, it is last
RUN dotnet publish ... --no-restore                 # only this rebuilds on a code change
```

The naive Dockerfile does `COPY . .` first, so *every* file change — a comment, a test, a README — invalidates the cache and forces a full restore. On a real service with dozens of packages, that is minutes per build, several times a day, for every developer.

Try it yourself:

```bash
bash scripts/layer-cache-demo.sh
# then change one comment in src/DockerDemo.Api/Program.cs and rebuild:
docker build -t docker-demo:prod -f Dockerfile .
# Watch the restore step print CACHED while publish rebuilds.
```

## Non-root, in one paragraph

Containers do not magically contain root. A process running as root inside a container that escapes its sandbox — through a kernel bug, a misconfigured mount, or an over-permissive runtime — is root on the host in the worst case. .NET 8 runtime images ship an unprivileged `app` user precisely so you do not have to create one: one line, `USER app`, before the entrypoint. Note the ordering — it must come *after* any step that needs root (like copying files into restricted paths), and it applies to everything after it, including the entrypoint.

## Distroless: smallest, safest, hardest to debug

Chiseled images contain the runtime and its shared libraries and almost nothing else: no shell, no package manager, no `curl`. That is the security win — there is nothing for an attacker to use if they get in, and far less to patch. The price is paid at 2 AM: you cannot `docker exec` into the container to look around, because there is no shell to exec. The working patterns are: debug in the regular runtime image (same code, same publish output, shell included), ship the chiseled image; or add temporary debug tooling through a sidecar container that shares the process namespace. Never "fix" a distroless debugging problem by going back to shipping the SDK image.

One more chiseled note: because there is no `curl` or `wget` in the final image, you cannot write a `HEALTHCHECK` that runs inside the container. That is why `docker-compose.yml` has none — health is checked from the host (`curl localhost:8080/health`). The `/health` endpoint itself is deliberately cheap (no database, no downstream call) so a slow dependency never makes a healthy container look dead.

Tag spelling matters: the Ubuntu chiseled tag is `8.0-noble-chiseled` (past tense). `8.0-noble-chisel` does not exist — the build fails at the image pull, which is exactly how this repo's first distroless build failed before the fix.

## .dockerignore: the step everyone skips

Without `.dockerignore`, `COPY . .` sends `bin/`, `obj/`, `.git/`, and possibly `.env` files into the build context — slower builds, bigger layers, and a real chance of shipping a secret. This repo's `.dockerignore` excludes build outputs, version-control state, editor files, and environment files. Treat a missing `.dockerignore` as a bug in code review, the same as a missing test.

## Exercises — break it on purpose

1. **Feel the naive tax.** Run `scripts/build-all.sh`. Then `docker run -it docker-demo:naive bash` and `whoami` — root, with a compiler, in "production."
2. **Bust the cache.** Move the `COPY . .` line above the restore in `Dockerfile`, rebuild twice, and watch restore run both times.
3. **Hit the distroless wall.** Run the distroless image, then try `docker exec -it <container> sh`. It fails — that failure *is* the feature. Now decide how you would debug a crash in it (answer in the section above).
4. **Leak something, then stop it.** Temporarily remove `.env` from `.dockerignore`, create a dummy `.env` with a fake key, build, and inspect the image layers. Then restore the ignore rule and rebuild. Never do this with a real secret.
5. **Add a second project.** Reference a class library from the API and fix the Dockerfile's restore step to copy both `.csproj` files before restoring — the multi-project version of the same caching rule.

## Project layout

```
Dockerfile                    # production: multi-stage, cached restore, non-root
Dockerfile.naive              # the common first attempt, kept for comparison
Dockerfile.distroless         # chiseled runtime: smallest, no shell
.dockerignore                 # keeps build outputs and secrets out of the context
docker-compose.yml            # one-command run of the production image
scripts/build-all.sh          # build all three images, print sizes
scripts/layer-cache-demo.sh   # guided demo of cached vs rebuilt layers
src/DockerDemo.Api/           # the Products API (Program.cs, ProductsStore.cs)
tests/DockerDemo.Tests/       # store unit tests + WebApplicationFactory API tests
.github/workflows/ci.yml      # build + test + build all images + container smoke tests
```

## Honest limits

- Image sizes in the table are relative, not promised: base images change with .NET servicing releases, and your numbers will differ. Measure yours.
- This repo containers a single service. Multi-project solutions, private NuGet feeds (which need credentials at restore time, never baked into layers), and multi-architecture builds (`--platform linux/amd64,linux/arm64`) are the natural next steps, not covered here.
- Chiseled images use Ubuntu Noble packages; if your app needs ICU/globalization data or native libraries beyond the runtime, verify them in the chiseled image — the absence of a shell makes missing natives fail late and confusingly.
- Docker is not verified on every machine here: CI builds all three images and smoke-tests two of them on every push, so the Dockerfiles are proven even where a local Docker daemon is unavailable.

## Next steps

- Deploy the production image to Azure Container Apps (one command, no cluster) and then to AKS — the queue's `aks-helm-deploy` topic picks up where this one stops.
- Add a GitHub Actions job that pushes the production image to a registry with a version tag on release, using OIDC instead of a stored password.
- Pair this with the [terraform-azure](https://github.com/varunkonnojutech-lab/terraform-azure) repo: the container is only half the deployment; the infrastructure it lands on is the other half.
