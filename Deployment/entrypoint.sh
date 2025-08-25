#!/usr/bin/env bash
set -euo pipefail

DEFAULT_CRT="/etc/ssl/certs/cert.crt"
DEFAULT_KEY="/etc/ssl/private/cert.key"

# Check if PEM pair is provided (mounted), otherwise generate self-signed cert
if [ -f "$DEFAULT_CRT" ] && [ -f "$DEFAULT_KEY" ]; then
  echo "Using existing PEM cert/key files"
  chmod 0640 "$DEFAULT_CRT" "$DEFAULT_KEY"
  chown root:root "$DEFAULT_CRT" "$DEFAULT_KEY"
else
  echo "No certificate found; generating a self-signed certificate"
  mkdir -p /etc/ssl/certs /etc/ssl/private
  openssl req -x509 -nodes -days 36500 -newkey rsa:2048 \
    -keyout "$DEFAULT_KEY" -out "$DEFAULT_CRT" \
    -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost"
  chmod 0640 "$DEFAULT_KEY" "$DEFAULT_CRT"
  chown root:root "$DEFAULT_KEY" "$DEFAULT_CRT"
fi

# Ensure supervisord logs dir exists
mkdir -p /var/log/supervisor
chown root:root /var/log/supervisor

# Exec supervisord (replace process)
exec /usr/bin/supervisord -c /etc/supervisor/conf.d/supervisord.conf
