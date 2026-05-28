# Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Etapa de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Instalar librerías LDAP necesarias para System.DirectoryServices.Protocols (Debian 12)
RUN apt-get update && \
    apt-get install -y libldap-2.5-0 libldap-common && \
    ldconfig && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/out .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SCNexus.Core.API.dll"]
