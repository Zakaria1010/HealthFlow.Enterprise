FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["HealthFlow.Services.Patients/HealthFlow.Services.Patients.csproj", "HealthFlow.Services.Patients/"]
COPY ["HealthFlow.Shared/HealthFlow.Shared.csproj", "HealthFlow.Shared/"]
RUN dotnet restore "HealthFlow.Services.Patients/HealthFlow.Services.Patients.csproj"
COPY . .
WORKDIR "/src/HealthFlow.Services.Patients"
RUN dotnet build "HealthFlow.Services.Patients.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HealthFlow.Services.Patients.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HealthFlow.Services.Patients.dll"]