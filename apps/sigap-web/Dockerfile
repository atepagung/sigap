# SEMENTARA — versi kerja untuk verifikasi lintas platform (PLAYBOOK P4.8), BUKAN Dockerfile production.
# Nanti dibandingkan/diganti Dockerfile standar dari starter.mfe.
#
# Konteks build = AKAR repo (bukan folder app): sigap-web memakai npm workspaces (package-lock.json akar),
# tsconfig.base.json, dan libs/keu-ui-dummy + libs/iam-dummy-web lewat alias paket.
#
#   docker build -f apps/sigap-web/Dockerfile -t sigap-web:verif .
#
# Remote MFE hanya berkas statis (remoteEntry.json + chunk) yang dimuat shell dari origin lain, jadi
# disajikan nginx dengan header CORS. [ASUMSI] cara platform menyajikan remote belum diketahui.

FROM node:24 AS build
WORKDIR /w
ENV HUSKY=0 CI=true
COPY . .
RUN npm ci --no-audit --no-fund \
 && npm run build:prod --workspace apps/sigap-web

FROM nginx:1.27-alpine AS runtime
COPY apps/sigap-web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /w/apps/sigap-web/dist/sigap-web/browser /usr/share/nginx/html
EXPOSE 8080
