#!/bin/sh
# Dijalankan entrypoint image nginx sebelum nginx mulai: menulis config.json dari environment
# (apps/sigap-web/.env.example). API_BASE_URL wajib — tanpa itu container menolak mulai, bukan diam-diam
# menyajikan aplikasi yang memanggil alamat pengembangan. Variabel lain boleh kosong.
set -eu

: "${API_BASE_URL:?API_BASE_URL wajib diisi: alamat sigap-api yang dijangkau peramban}"

mkdir -p /tmp/sigap
envsubst '${API_BASE_URL} ${SSO_ISSUER_URL} ${SSO_CLIENT_ID} ${VAPID_PUBLIC_KEY}' \
  < /etc/sigap/config.json.template > /tmp/sigap/config.json
