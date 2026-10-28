#!/usr/bin/env bash
set -Eeuo pipefail

readonly SERVICE_NAME="merchant-bot"
readonly SERVICE_USER="merchant-bot"
readonly INSTALL_ROOT="/opt/${SERVICE_NAME}"
readonly APP_DIR="${INSTALL_ROOT}/app"
readonly STATE_DIR="/var/lib/${SERVICE_NAME}"
readonly CONFIG_DIR="/etc/${SERVICE_NAME}"
readonly ENV_FILE="${CONFIG_DIR}/${SERVICE_NAME}.env"
readonly UNIT_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly PUBLISH_DIR="${1:-${SCRIPT_DIR}/publish}"

die() {
    printf 'ERROR: %s\n' "$*" >&2
    exit 1
}

log() {
    printf '[%s] %s\n' "$SERVICE_NAME" "$*"
}

[[ "${EUID}" -eq 0 ]] || die "Run this installer as root, for example: sudo $0"
command -v systemctl >/dev/null 2>&1 || die "systemd/systemctl is required."
[[ -d /run/systemd/system ]] || die "systemd is not running as PID 1."
command -v dotnet >/dev/null 2>&1 || die ".NET runtime is missing; install .NET 10 first."
dotnet --list-runtimes | grep -q '^Microsoft\.NETCore\.App 10\.' \
    || die ".NET 10 runtime is required to run this framework-dependent publish."
[[ -x "${PUBLISH_DIR}/KeyBot" ]] || die "Published executable not found: ${PUBLISH_DIR}/KeyBot"

if dotnet --list-sdks | grep -q '^10\.'; then
    log ".NET 10 SDK detected."
else
    log ".NET SDK not installed; this is fine for production because the app is already published."
fi

command -v psql >/dev/null 2>&1 \
    && log "PostgreSQL client detected (not required; this app currently uses a JSON file)." \
    || log "PostgreSQL client not found (not required; this app currently uses a JSON file)."
command -v sqlite3 >/dev/null 2>&1 \
    && log "SQLite detected (not required; this app currently uses a JSON file)." \
    || log "SQLite not found (not required; this app currently uses a JSON file)."

if [[ ! -t 0 ]]; then
    die "Interactive configuration is required on first install; run from a terminal."
fi

if ! id -u "${SERVICE_USER}" >/dev/null 2>&1; then
    if [[ -x /usr/sbin/nologin ]]; then
        useradd --system --home-dir "${STATE_DIR}" --shell /usr/sbin/nologin "${SERVICE_USER}"
    else
        useradd --system --home-dir "${STATE_DIR}" --shell /sbin/nologin "${SERVICE_USER}"
    fi
fi

install -d -o root -g root -m 0755 "${INSTALL_ROOT}"
install -d -o "${SERVICE_USER}" -g "${SERVICE_USER}" -m 0750 "${STATE_DIR}"
install -d -o root -g root -m 0750 "${CONFIG_DIR}"

read_secret() {
    local prompt="$1"
    local result
    read -r -s -p "${prompt}: " result
    printf '\n' >&2
    REPLY="${result}"
}

read_value() {
    local prompt="$1"
    local default_value="$2"
    local result
    read -r -p "${prompt} [${default_value}]: " result
    REPLY="${result:-${default_value}}"
}

escape_env_value() {
    local value="$1"
    value="${value//\\/\\\\}"
    value="${value//\"/\\\"}"
    printf '"%s"' "${value}"
}

write_env_entry() {
    printf '%s=%s\n' "$1" "$(escape_env_value "$2")" >>"${ENV_TEMP}"
}

if [[ ! -f "${ENV_FILE}" ]]; then
    log "Creating protected environment file ${ENV_FILE}."
    read_secret "Telegram bot token"
    BOT_TOKEN="${REPLY}"
    [[ -n "${BOT_TOKEN}" ]] || die "BOT_TOKEN cannot be empty."

    read_value "Admin Telegram user IDs (comma-separated)" ""
    ADMIN_IDS="${REPLY}"
    [[ -n "${ADMIN_IDS}" ]] || die "At least one ADMIN_IDS value is required."

    read_secret "CryptoPay API token (leave empty to disable real payment links)"
    CRYPTOPAY_TOKEN="${REPLY}"
    CRYPTOPAY_WEBHOOK_URL=""
    if [[ -n "${CRYPTOPAY_TOKEN}" ]]; then
        read_value "Public HTTPS CryptoPay webhook URL" ""
        CRYPTOPAY_WEBHOOK_URL="${REPLY}"
        [[ "${CRYPTOPAY_WEBHOOK_URL}" =~ ^https://[^/?#]+/webhooks/cryptopay$ ]] \
            || die "Set CRYPTOPAY_WEBHOOK_URL to your public HTTPS webhook URL."
    fi
    read_secret "OpenRouter API key (optional; leave empty to disable AI)"
    OPENROUTER_API_KEY="${REPLY}"
    read_value "Bot username without @" "bot"
    BOT_USERNAME="${REPLY}"
    read_value "Support username without @" ""
    SUPPORT_USERNAME="${REPLY}"
    read_value "Terms URL" "https://telegra.ph"
    TERMS_URL="${REPLY}"

    ENV_TEMP="$(mktemp "${CONFIG_DIR}/.${SERVICE_NAME}.env.XXXXXX")"
    chmod 0600 "${ENV_TEMP}"
    write_env_entry "BOT_TOKEN" "${BOT_TOKEN}"
    write_env_entry "ADMIN_IDS" "${ADMIN_IDS}"
    write_env_entry "CRYPTOPAY_TOKEN" "${CRYPTOPAY_TOKEN}"
    write_env_entry "CRYPTOPAY_WEBHOOK_URL" "${CRYPTOPAY_WEBHOOK_URL}"
    write_env_entry "CRYPTOPAY_WEBHOOK_LISTEN_URL" "http://127.0.0.1:8081"
    write_env_entry "ALLOW_TEST_PAYMENTS" "false"
    write_env_entry "OPENROUTER_API_KEY" "${OPENROUTER_API_KEY}"
    write_env_entry "BOT_USERNAME" "${BOT_USERNAME}"
    write_env_entry "SUPPORT_USERNAME" "${SUPPORT_USERNAME}"
    write_env_entry "TERMS_URL" "${TERMS_URL}"
    write_env_entry "DATA_PATH" "${STATE_DIR}/bot_data.json"
    mv -f -- "${ENV_TEMP}" "${ENV_FILE}"
    chmod 0600 "${ENV_FILE}"
    chown root:root "${ENV_FILE}"
else
    log "Existing environment file is preserved."
    chmod 0600 "${ENV_FILE}"
    chown root:root "${ENV_FILE}"
fi

readonly WORK_DIR="$(mktemp -d "${INSTALL_ROOT}/.install.XXXXXX")"
readonly STAGING_DIR="${WORK_DIR}/app-staging"
readonly BACKUP_DIR="${WORK_DIR}/app-previous"
readonly UNIT_BACKUP="${WORK_DIR}/unit-backup"
readonly ENV_BACKUP="${WORK_DIR}/env-backup"
readonly UNIT_TEMP="${WORK_DIR}/unit-temp"

APP_BACKED_UP=0
APP_INSTALLED=0
UNIT_EXISTED=0
ENV_EXISTED=0
WAS_ACTIVE=0
WAS_ENABLED=0

if systemctl is-active --quiet "${SERVICE_NAME}"; then WAS_ACTIVE=1; fi
if systemctl is-enabled --quiet "${SERVICE_NAME}"; then WAS_ENABLED=1; fi
if [[ -f "${UNIT_FILE}" ]]; then
    UNIT_EXISTED=1
    cp -p -- "${UNIT_FILE}" "${UNIT_BACKUP}"
fi
if [[ -f "${ENV_FILE}" ]]; then
    ENV_EXISTED=1
    cp -p -- "${ENV_FILE}" "${ENV_BACKUP}"
fi

rollback() {
    local status=$?
    trap - ERR EXIT INT TERM
    if [[ "${status}" -eq 0 ]]; then
        return
    fi

    log "Installation failed; rolling back changed files."
    set +e
    if [[ "${APP_INSTALLED}" -eq 1 && -d "${APP_DIR}" ]]; then
        rm -rf -- "${APP_DIR}"
    fi
    if [[ "${APP_BACKED_UP}" -eq 1 && -d "${BACKUP_DIR}" ]]; then
        mv -- "${BACKUP_DIR}" "${APP_DIR}"
    fi
    if [[ "${UNIT_EXISTED}" -eq 1 && -f "${UNIT_BACKUP}" ]]; then
        cp -p -- "${UNIT_BACKUP}" "${UNIT_FILE}"
    else
        rm -f -- "${UNIT_FILE}"
    fi
    if [[ "${ENV_EXISTED}" -eq 1 && -f "${ENV_BACKUP}" ]]; then
        cp -p -- "${ENV_BACKUP}" "${ENV_FILE}"
    else
        rm -f -- "${ENV_FILE}"
    fi
    rm -rf -- "${WORK_DIR}"
    systemctl daemon-reload
    if [[ "${WAS_ACTIVE}" -eq 1 ]]; then
        systemctl start "${SERVICE_NAME}"
    else
        systemctl stop "${SERVICE_NAME}"
    fi
    if [[ "${WAS_ENABLED}" -eq 0 ]]; then
        systemctl disable "${SERVICE_NAME}"
    fi
    exit "${status}"
}
trap rollback ERR EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

install -d -o root -g root -m 0750 "${STAGING_DIR}"
cp -a -- "${PUBLISH_DIR}/." "${STAGING_DIR}/"
chown -R "${SERVICE_USER}:${SERVICE_USER}" "${STAGING_DIR}"
find "${STAGING_DIR}" -type d -exec chmod 0750 {} +
find "${STAGING_DIR}" -type f -exec chmod 0640 {} +
chmod 0750 "${STAGING_DIR}/KeyBot"

if [[ -d "${APP_DIR}" ]]; then
    mv -- "${APP_DIR}" "${BACKUP_DIR}"
    APP_BACKED_UP=1
fi
mv -- "${STAGING_DIR}" "${APP_DIR}"
APP_INSTALLED=1

cat >"${UNIT_TEMP}" <<EOF
[Unit]
Description=Merchant Telegram VPN Bot
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=${SERVICE_USER}
Group=${SERVICE_USER}
WorkingDirectory=${STATE_DIR}
EnvironmentFile=${ENV_FILE}
ExecStart=${APP_DIR}/KeyBot
Restart=always
RestartSec=5
UMask=0027
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ProtectKernelTunables=true
ProtectKernelModules=true
ProtectControlGroups=true
RestrictSUIDSGID=true
LockPersonality=true
CapabilityBoundingSet=
AmbientCapabilities=
ReadWritePaths=${STATE_DIR}

[Install]
WantedBy=multi-user.target
EOF
chmod 0644 "${UNIT_TEMP}"
mv -f -- "${UNIT_TEMP}" "${UNIT_FILE}"

systemctl daemon-reload
systemctl enable "${SERVICE_NAME}"
systemctl restart "${SERVICE_NAME}"
systemctl is-active --quiet "${SERVICE_NAME}"

trap - ERR
rm -rf -- "${WORK_DIR}"
log "Installation complete. Check logs with: journalctl -u ${SERVICE_NAME} -f"
