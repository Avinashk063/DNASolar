# -------- BUILD STAGE --------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY DNA-Softech.Web/*.csproj DNA-Softech.Web/
COPY DNA-Softech.Application/*.csproj DNA-Softech.Application/
COPY DNA-Softech.Domain/*.csproj DNA-Softech.Domain/
COPY DNA-Softech.Infrastructure/*.csproj DNA-Softech.Infrastructure/

# Restore using Web project
RUN dotnet restore DNA-Softech.Web/DNA-Softech.Web.csproj

# Copy full source
COPY . .

# Publish
WORKDIR /src/DNA-Softech.Web
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false


# -------- RUNTIME STAGE --------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:${PORT}

EXPOSE 8080

ENTRYPOINT ["dotnet", "DNA-Softech.Web.dll"]