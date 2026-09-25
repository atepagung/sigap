# SEMENTARA — versi kerja untuk verifikasi lintas platform (PLAYBOOK P4.8), BUKAN Dockerfile production.
# Nanti dibandingkan/diganti Dockerfile standar dari starter.mfe.
#
# Konteks build = AKAR repo (bukan folder app): sigap-api merujuk libs/iam-dummy dan libs/notifikasi lewat
# ProjectReference. Setelah dummy diganti paket platform asli, konteksnya dapat dipersempit ke folder app.
#
#   docker build -f apps/sigap-api/Dockerfile -t sigap-api:verif .
#   node scripts/verifikasi-container.mjs        # membangun, menjalankan, dan memeriksa keduanya
#
# Memakai `dotnet build`, BUKAN `dotnet publish`: publish sengaja digagalkan (SIGAP001, aturan dummy #4)
# selama libs/iam-dummy masih ter-resolve. Image ini tidak boleh naik ke production.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ENV DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
COPY . .
RUN dotnet build apps/sigap-api/src/Sigap.Api/Sigap.Api.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
# ConnectionStrings__Sigap, Iam__Authority, dan Lampiran__Folder wajib diberikan saat `docker run`
# (proses menolak mulai bila kosong); tidak ada nilai rahasia di image.
ENV ASPNETCORE_URLS=http://+:8080 DOTNET_NOLOGO=1
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Sigap.Api.dll"]
