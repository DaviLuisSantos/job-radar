#!/usr/bin/env bash
# One-time setup of a Debian (12/13, amd64) host for JobRadar: Docker, PowerShell 7, the .NET SDK, git, cron and
# (optionally) Tailscale; then the checkout, the .env, the cron jobs and the first deploy. Safe to run again.
#
#   curl -fsSL https://raw.githubusercontent.com/DaviLuisSantos/job-radar/main/deploy/setup-debian.sh -o setup.sh
#   bash setup.sh --profiles ~/appsettings.Local.json [--parity ~/parity.json] [--restore ~/jobradar.dump]
#
# Run as the user that will own the stack, not root (it uses sudo for packages). docs/producao.md has the rest.
set -euo pipefail

REPO_URL="https://github.com/DaviLuisSantos/job-radar.git"
APP_DIR="/srv/job-radar"
DATA_DIR="/srv/jobradar"
TIMEZONE="America/Sao_Paulo"
PROFILES_SRC=""
SCORING_SRC=""
PARITY_SRC=""
RESTORE_DUMP=""
WITH_TAILSCALE=1
RUN_DEPLOY=1
FORCE_ENV=0

usage() {
    cat <<EOF
Usage: setup-debian.sh [options]
  --profiles FILE     appsettings.Local.json to install (required the first time)
  --scoring FILE      scoring-cases.Local.json (optional; checked by the deploy's parity step)
  --parity FILE       parity file for the scoring check (optional)
  --restore DUMP      restore a deploy/backup.ps1 dump into an empty database before the first deploy
  --timezone ZONE     IANA zone for the site's times (default $TIMEZONE)
  --dir PATH          checkout (default $APP_DIR)
  --repo URL          git remote (default $REPO_URL)
  --no-tailscale      skip Tailscale (site only on localhost)
  --no-deploy         set everything up but do not run deploy.ps1
  --force-env         overwrite the machine settings already in .env (by default only missing ones are filled in)
EOF
}

while [ $# -gt 0 ]; do
    case "$1" in
        --profiles) PROFILES_SRC="$2"; shift 2 ;;
        --scoring) SCORING_SRC="$2"; shift 2 ;;
        --parity) PARITY_SRC="$2"; shift 2 ;;
        --restore) RESTORE_DUMP="$2"; shift 2 ;;
        --timezone) TIMEZONE="$2"; shift 2 ;;
        --dir) APP_DIR="$2"; shift 2 ;;
        --repo) REPO_URL="$2"; shift 2 ;;
        --no-tailscale) WITH_TAILSCALE=0; shift ;;
        --no-deploy) RUN_DEPLOY=0; shift ;;
        --force-env) FORCE_ENV=1; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
    esac
done

log() { printf '\n== %s\n' "$*"; }
die() { echo "ERROR: $*" >&2; exit 1; }

[ "$(id -u)" -ne 0 ] || die "Run as the normal user that will own the stack (not root); it calls sudo itself."
[ "$(dpkg --print-architecture)" = "amd64" ] || die "Only amd64 is supported: the Dockerfile builds the migration bundle for linux-x64."
command -v sudo >/dev/null || die "sudo is missing: as root run 'apt install sudo' and add this user to the sudo group."
for f in "$PROFILES_SRC" "$SCORING_SRC" "$PARITY_SRC" "$RESTORE_DUMP"; do
    [ -z "$f" ] || [ -f "$f" ] || die "File not found: $f"
done
[ -n "$PROFILES_SRC" ] || [ -f "$DATA_DIR/appsettings.Local.json" ] || die "Pass --profiles <appsettings.Local.json> (not in git; copy it from the old machine)."

# ── Packages ────────────────────────────────────────────────────────────────────────────────────────────────────────
log "Base packages"
sudo apt-get update -qq
sudo apt-get install -y -qq ca-certificates curl wget git cron gnupg

if ! command -v docker >/dev/null; then
    log "Docker"
    curl -fsSL https://get.docker.com | sudo sh
fi
sudo systemctl enable --now docker
if ! docker compose version >/dev/null 2>&1; then
    log "Docker Compose v2 plugin"
    sudo apt-get install -y -qq docker-compose-plugin || sudo apt-get install -y -qq docker-compose-v2 \
        || die "Docker is installed without Compose v2 (docker.io from Debian?). Remove it ('sudo apt remove docker.io') and run again."
    docker compose version >/dev/null 2>&1 || die "'docker compose' still does not work after installing the plugin."
fi
if ! id -nG "$USER" | tr ' ' '\n' | grep -qx docker; then
    sudo usermod -aG docker "$USER"
fi
# The new group is not in this shell yet: continue under it instead of asking for a logout.
if ! docker info >/dev/null 2>&1; then
    log "Continuing with the docker group"
    exec sg docker -c "bash $(printf '%q' "$0") $(printf '%q ' "$@")"
fi

has_sdk10() { command -v dotnet >/dev/null && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; }
if ! command -v pwsh >/dev/null || ! has_sdk10; then
    log "PowerShell 7 and the .NET 10 SDK (Microsoft repository)"
    . /etc/os-release
    tmp="$(mktemp -d)"
    wget -q "https://packages.microsoft.com/config/debian/${VERSION_ID}/packages-microsoft-prod.deb" -O "$tmp/ms.deb"
    sudo dpkg -i "$tmp/ms.deb"
    rm -rf "$tmp"
    sudo apt-get update -qq
    command -v pwsh >/dev/null || sudo apt-get install -y -qq powershell
    if ! has_sdk10 && ! sudo apt-get install -y -qq dotnet-sdk-10.0; then
        log "dotnet-sdk-10.0 is not in the repository: using Microsoft's install script"
        curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
        sudo bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet
        # /usr/local/bin comes before /usr/bin: wins over an older dotnet already installed
        sudo ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet
        hash -r
    fi
fi
command -v pwsh >/dev/null || die "PowerShell 7 is not available after the install."
has_sdk10 || die "No .NET 10 SDK found ('dotnet --list-sdks'): install it and run again."

if [ "$WITH_TAILSCALE" -eq 1 ]; then
    if ! command -v tailscale >/dev/null; then
        log "Tailscale"
        curl -fsSL https://tailscale.com/install.sh | sh
    fi
    if ! tailscale ip -4 >/dev/null 2>&1; then
        log "Tailscale needs a login: open the link below, then run this script again."
        sudo tailscale up
    fi
fi

# ── Checkout and machine files ──────────────────────────────────────────────────────────────────────────────────────
log "Checkout in $APP_DIR"
sudo mkdir -p "$APP_DIR" "$DATA_DIR/state"
sudo chown -R "$USER":"$USER" "$APP_DIR" "$DATA_DIR"
if [ -d "$APP_DIR/.git" ]; then
    git -C "$APP_DIR" pull --ff-only \
        || die "Could not fast-forward $APP_DIR (local changes or commits?). Fix it there ('git status') and run again."
elif [ -z "$(ls -A "$APP_DIR")" ]; then
    git clone "$REPO_URL" "$APP_DIR"
else
    die "$APP_DIR exists, is not empty and is not a git checkout. Use --dir or empty it."
fi

install_file() { # src dest: the container user must be able to read it
    install -m 644 "$1" "$2"
}
[ -z "$PROFILES_SRC" ] || install_file "$PROFILES_SRC" "$DATA_DIR/appsettings.Local.json"
[ -z "$SCORING_SRC" ] || install_file "$SCORING_SRC" "$DATA_DIR/scoring-cases.Local.json"
[ -z "$PARITY_SRC" ] || install_file "$PARITY_SRC" "$DATA_DIR/parity.json"

# key value: set it when missing or empty; an existing value stays unless --force-env (the rest of .env is kept)
set_env() {
    if [ "$FORCE_ENV" -eq 0 ] && [ "$ENV_EXISTED" -eq 1 ] && grep -qE "^$1=.+" "$APP_DIR/.env"; then
        return
    fi
    if grep -qE "^$1=" "$APP_DIR/.env"; then
        sed -i "s|^$1=.*|$1=$2|" "$APP_DIR/.env"
    else
        echo "$1=$2" >> "$APP_DIR/.env"
    fi
}
ENV_EXISTED=1
if [ ! -f "$APP_DIR/.env" ]; then
    ENV_EXISTED=0 # the example's values are placeholders: this run fills them in
    cp "$APP_DIR/deploy/prod.env.example" "$APP_DIR/.env"
fi
set_env JOBRADAR_PROFILES "$DATA_DIR/appsettings.Local.json"
set_env JOBRADAR_STATE "$DATA_DIR/state"
set_env JOBRADAR_TIMEZONE "$TIMEZONE"
[ ! -f "$DATA_DIR/parity.json" ] || set_env JOBRADAR_PARITY_FILE "$DATA_DIR/parity.json"
hosts="localhost"
if command -v tailscale >/dev/null && ts_ip="$(tailscale ip -4 2>/dev/null | head -1)" && [ -n "$ts_ip" ]; then
    hosts="$hosts;$ts_ip"
    ts_name="$(tailscale status --json 2>/dev/null | sed -n 's/.*"DNSName": *"\([^"]*\)\.".*/\1/p' | head -1)"
    [ -z "$ts_name" ] || hosts="$hosts;$ts_name"
    set_env JOBRADAR_TAILSCALE_IP "$ts_ip"
fi
set_env JOBRADAR_ALLOWED_HOSTS "$hosts"
log ".env ready (hosts: $hosts)"

# ── Database restore (optional) ─────────────────────────────────────────────────────────────────────────────────────
compose() { docker compose -p job-radar-au -f "$APP_DIR/docker-compose.yml" --env-file "$APP_DIR/.env" "$@"; }
if [ -n "$RESTORE_DUMP" ]; then
    log "Restoring $RESTORE_DUMP"
    compose up -d --wait postgres
    if compose exec -T postgres psql -U jobradar -d jobradar -tAc "select to_regclass('public.collection_runs')" | grep -q collection_runs; then
        die "The database already has data. Not restoring over it (drop the volume job-radar-au_pgdata to start empty)."
    fi
    compose cp "$RESTORE_DUMP" postgres:/tmp/restore.dump
    compose exec -T postgres pg_restore -U jobradar -d jobradar --clean --if-exists /tmp/restore.dump
    compose exec -T postgres rm -f /tmp/restore.dump
fi

# ── Cron ────────────────────────────────────────────────────────────────────────────────────────────────────────────
log "Cron jobs"
PWSH="$(command -v pwsh)"
{
    crontab -l 2>/dev/null | grep -v '# jobradar' || true
    # cron's own PATH lacks /usr/local/bin (dotnet may live there)
    echo "PATH=/usr/local/bin:/usr/bin:/bin # jobradar"
    echo "*/15 * * * * cd $APP_DIR && $PWSH -NoProfile -File deploy/auto-deploy.ps1 >/dev/null 2>&1 # jobradar"
    echo "*/15 * * * * cd $APP_DIR && $PWSH -NoProfile -File deploy/watchdog.ps1 >/dev/null 2>&1 # jobradar"
    echo "0 23 * * * cd $APP_DIR && $PWSH -NoProfile -File deploy/backup.ps1 >/dev/null 2>&1 # jobradar"
} | crontab -
sudo systemctl enable --now cron

# ── First deploy ────────────────────────────────────────────────────────────────────────────────────────────────────
if [ "$RUN_DEPLOY" -eq 1 ]; then
    log "First deploy (builds the images; takes a few minutes)"
    cd "$APP_DIR"
    "$PWSH" -NoProfile -File deploy/deploy.ps1
    echo
    echo "Done. Status page: http://localhost:5380/status"
else
    echo
    echo "Done. Deploy when ready: cd $APP_DIR && pwsh deploy/deploy.ps1"
fi
