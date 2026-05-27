# Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Etapa de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Instalar librerías LDAP necesarias para System.DirectoryServices.Protocols
RUN apt-get update && \
    apt-get install -y libldap2 && \
    ln -s /usr/lib/x86_64-linux-gnu/libldap-2.5.so.0 /usr/lib/x86_64-linux-gnu/libldap-2.5.so.0.so 2>/dev/null || true && \
    ldconfig && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/out .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SCNexus.Core.API.dll"]
