# syntax=docker/dockerfile:1
#
# Image sigap-api (.NET 10, port 8080, health check GET /health/live dan /health/ready — UR, tabel container).
# [ASUMSI] Ditulis sendiri karena starter.mfe/standar platform belum tersedia; diganti bila template membawa
# Dockerfile resmi (LEMBAR_PEMBANDING_DUMMY bagian 8).
#
# Dua target:
#   production  (bawaan)  dotnet publish. SENGAJA GAGAL (SIGAP001) selama libs/iam-dummy ter-resolve — aturan
#                         dummy #4: tidak ada image production berisi dummy.
#   dev                   dotnet build. Hanya untuk namespace sigap-dev (keputusan pemilik 28 Sep 2026); keamanan
#                         tiga lapisnya masih iam-dummy.
#
#   docker build -f apps/sigap-api/Dockerfile --target dev -t sigap-api:dev .
#
# Konteks = akar repo, bukan folder app (selisih dengan syarat UR, DUMMY_REGISTRY bagian 9 butir 25): proyek
# merujuk libs/iam-dummy dan libs/notifikasi lewat ProjectReference. Hanya jalur di bawah ini yang masuk image.
# Tidak ada nilai konfigurasi di image; semuanya dari environment (apps/sigap-api/.env.example).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS sumber
WORKDIR /src
ENV DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
COPY apps/sigap-api apps/sigap-api
COPY libs/iam-dummy libs/iam-dummy
COPY libs/notifikasi libs/notifikasi
RUN dotnet restore apps/sigap-api/src/Sigap.Api/Sigap.Api.csproj -p:Configuration=Release

FROM sumber AS build-dev
RUN dotnet build apps/sigap-api/src/Sigap.Api/Sigap.Api.csproj -c Release --no-restore -o /out

FROM sumber AS build-production
RUN dotnet publish apps/sigap-api/src/Sigap.Api/Sigap.Api.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 DOTNET_NOLOGO=1
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Sigap.Api.dll"]

FROM runtime AS dev
COPY --from=build-dev /out .

FROM runtime AS production
COPY --from=build-production /out .
