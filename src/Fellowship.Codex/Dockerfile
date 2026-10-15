FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/ ./src/
RUN dotnet restore src/Fellowship.Codex/Fellowship.Codex.csproj
RUN dotnet publish src/Fellowship.Codex/Fellowship.Codex.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app ./
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Fellowship.Codex.dll"]
