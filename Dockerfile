FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine as build
WORKDIR /source
COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish PlanningService.csproj --no-restore -o /app


FROM mcr.microsoft.com/dotnet/aspnet:7.0-alpine
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["./PlanningService"]

