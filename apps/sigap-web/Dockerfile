# syntax=docker/dockerfile:1
#
# Image sigap-web: Angular hasil build, berkas statis, port 8080, health check GET /healthz (UR, tabel container).
# [ASUMSI] Ditulis sendiri karena starter.mfe/standar platform belum tersedia; diganti bila template membawa
# Dockerfile resmi (LEMBAR_PEMBANDING_DUMMY bagian 8).
#
#   docker build -f apps/sigap-web/Dockerfile -t sigap-web:dev .
#   docker run -p 8080:8080 -e API_BASE_URL=https://… sigap-web:dev
#
# Konteks = akar repo, bukan folder app (selisih dengan syarat UR, DUMMY_REGISTRY bagian 9 butir 25): npm
# workspaces (lockfile di akar), tsconfig.base.json, dan @danarakca/* yang masih dummy di libs/. Hanya jalur di
# bawah ini yang masuk image. Konfigurasi per lingkungan dari environment saat container mulai (config.json),
# jadi satu image untuk semua lingkungan (apps/sigap-web/.env.example).

FROM node:24 AS build
WORKDIR /w
ENV HUSKY=0 CI=true
COPY package.json package-lock.json tsconfig.base.json ./
# Anggota workspaces lain hanya package.json-nya, supaya `npm ci` cocok dengan lockfile.
COPY apps/shell-dummy/package.json apps/shell-dummy/package.json
COPY apps/sigap-web apps/sigap-web
COPY libs/keu-ui-dummy libs/keu-ui-dummy
COPY libs/iam-dummy-web libs/iam-dummy-web
RUN npm ci --no-audit --no-fund \
 && npm run build:prod --workspace apps/sigap-web

FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime
COPY apps/sigap-web/docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY apps/sigap-web/docker/config.json.template /etc/sigap/config.json.template
COPY --chmod=0755 apps/sigap-web/docker/40-sigap-config.sh /docker-entrypoint.d/40-sigap-config.sh
COPY --from=build /w/apps/sigap-web/dist/sigap-web/browser /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO /dev/null http://127.0.0.1:8080/healthz || exit 1
