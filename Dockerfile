# Estágio de Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["RevoltaLetrinhas.Api/RevoltaLetrinhas.Api.csproj", "RevoltaLetrinhas.Api/"]
RUN dotnet restore "RevoltaLetrinhas.Api/RevoltaLetrinhas.Api.csproj"

COPY . .
WORKDIR "/src/RevoltaLetrinhas.Api"
RUN dotnet publish "RevoltaLetrinhas.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio Final / Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "RevoltaLetrinhas.Api.dll"]
