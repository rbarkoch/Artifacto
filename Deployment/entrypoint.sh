#!/usr/bin/env bash
set -euo pipefail

DEFAULT_PFX="/etc/ssl/certs/cert.pfx"
DEFAULT_CRT="/etc/ssl/certs/cert.crt"
DEFAULT_KEY="/etc/ssl/private/cert.key"
PASSWORD="${CERT_PASSWORD:-changeit}"
USER_PFX_PATH="${CERT_PFX_PATH:-}"

# helper: copy pfx and set secure perms
copy_pfx() {
  local src="$1"
  cp "$src" "$DEFAULT_PFX"
  chmod 0640 "$DEFAULT_PFX"
  chown root:root "$DEFAULT_PFX"
}

# 1) If user supplied a PFX path via env and file exists, use it
if [ -n "$USER_PFX_PATH" ] && [ -f "$USER_PFX_PATH" ]; then
  echo "Using PFX from CERT_PFX_PATH: $USER_PFX_PATH"
  copy_pfx "$USER_PFX_PATH"

# 2) If docker secret mounted at /run/secrets/cert.pfx
elif [ -f "/run/secrets/cert.pfx" ]; then
  echo "Using PFX from /run/secrets/cert.pfx"
  copy_pfx "/run/secrets/cert.pfx"

# 3) If PEM pair is provided (mounted), convert to PFX
elif [ -f "$DEFAULT_CRT" ] && [ -f "$DEFAULT_KEY" ]; then
  echo "Converting mounted PEM cert/key to PFX"
  openssl pkcs12 -export -out "$DEFAULT_PFX" -inkey "$DEFAULT_KEY" -in "$DEFAULT_CRT" -passout pass:"$PASSWORD"
  chmod 0640 "$DEFAULT_PFX"
  chown root:root "$DEFAULT_PFX"

# 4) Otherwise generate a self-signed cert at runtime
else
  echo "No user certificate found; generating a self-signed certificate"
  mkdir -p /etc/ssl/certs /etc/ssl/private
  openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
    -keyout "$DEFAULT_KEY" -out "$DEFAULT_CRT" \
    -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost"
  chmod 0640 "$DEFAULT_KEY" "$DEFAULT_CRT"
  chown root:root "$DEFAULT_KEY" "$DEFAULT_CRT"
  openssl pkcs12 -export -out "$DEFAULT_PFX" -inkey "$DEFAULT_KEY" -in "$DEFAULT_CRT" -passout pass:"$PASSWORD"
  chmod 0640 "$DEFAULT_PFX"
  chown root:root "$DEFAULT_PFX"
fi

# Ensure supervisord logs dir exists
mkdir -p /var/log/supervisor
chown root:root /var/log/supervisor

# Ensure nginx expects the certificate names used in nginx.conf
# nginx.conf expects /etc/ssl/certs/cert.crt and /etc/ssl/private/cert.key
mkdir -p /etc/ssl/certs /etc/ssl/private
if [ -f "$DEFAULT_PFX" ]; then
  echo "Preparing nginx PEM files from PFX"
  # extract certificate (no key)
  openssl pkcs12 -in "$DEFAULT_PFX" -nokeys -clcerts -out /etc/ssl/certs/cert.crt -passin pass:"$PASSWORD" || true
  # extract private key
  openssl pkcs12 -in "$DEFAULT_PFX" -nocerts -nodes -out /etc/ssl/private/cert.key -passin pass:"$PASSWORD" || true
  chmod 0640 /etc/ssl/certs/cert.crt /etc/ssl/private/cert.key || true
  chown root:root /etc/ssl/certs/cert.crt /etc/ssl/private/cert.key || true
fi

# If PEM pair already exists, ensure they're in the correct location for nginx
if [ -f "$DEFAULT_CRT" ] && [ -f "$DEFAULT_KEY" ]; then
  echo "Copying PEM cert/key to nginx expected locations"
  # Only copy if they're not already in the right place
  if [ "$DEFAULT_CRT" != "/etc/ssl/certs/cert.crt" ]; then
    cp -f "$DEFAULT_CRT" /etc/ssl/certs/cert.crt
  fi
  if [ "$DEFAULT_KEY" != "/etc/ssl/private/cert.key" ]; then
    cp -f "$DEFAULT_KEY" /etc/ssl/private/cert.key
  fi
  chmod 0640 /etc/ssl/certs/cert.crt /etc/ssl/private/cert.key
  chown root:root /etc/ssl/certs/cert.crt /etc/ssl/private/cert.key
fi

# Exec supervisord (replace process)
exec /usr/bin/supervisord -c /etc/supervisor/conf.d/supervisord.conf
