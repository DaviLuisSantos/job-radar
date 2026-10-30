# Production images: the same on any host with Docker (docker-compose.yml, profile "prod"; docs/producao.md).
# One build stage, three runtime targets: worker, web and migrate (the EF Core bundle, applied before both start).
# Build with the commit, so the site's /status names what it runs:
#   docker compose --profile prod build --build-arg COMMIT=$(git rev-parse HEAD)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG COMMIT=unknown
WORKDIR /src
COPY . .
RUN dotnet publish src/JobRadar.Worker -c Release -o /out/worker -p:SourceRevisionId=${COMMIT} \
 && dotnet publish src/JobRadar.Web -c Release -o /out/web -p:SourceRevisionId=${COMMIT}
# Neither published project references the migrations: restore them for the bundle (NETSDK1004 otherwise).
RUN dotnet tool restore \
 && dotnet restore src/JobRadar.Infrastructure.Migrations.Postgres \
 && dotnet ef migrations bundle --project src/JobRadar.Infrastructure.Migrations.Postgres \
      --self-contained -r linux-x64 -o /out/migrate/migrate --configuration Release

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrate
COPY --from=build /out/migrate /srv/migrate
# The bundle's design-time factory reads JOBRADAR_CONNECTION.
ENTRYPOINT ["/srv/migrate/migrate"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS worker
COPY --from=build /out/worker /srv/app/worker
WORKDIR /srv/app/worker
USER $APP_UID
ENTRYPOINT ["dotnet", "JobRadar.Worker.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS web
COPY --from=build /out/web /srv/app/web
# The site reads the deploy record next to its folder (Deployment.Read): /srv/app/deployed.txt, from the host's
# state folder mounted at /srv/state.
RUN ln -s /srv/state/deployed.txt /srv/app/deployed.txt
WORKDIR /srv/app/web
USER $APP_UID
ENV Urls=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "JobRadar.Web.dll"]
