FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/NovaWallet.Api/NovaWallet.Api.csproj src/NovaWallet.Api/
RUN dotnet restore src/NovaWallet.Api/NovaWallet.Api.csproj
COPY src/ src/
RUN dotnet publish src/NovaWallet.Api/NovaWallet.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "NovaWallet.Api.dll"]
