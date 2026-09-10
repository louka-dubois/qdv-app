FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json ./
COPY QDVapp/*.csproj ./QDVapp/
RUN dotnet restore QDVapp/QDVapp.csproj

COPY . .
RUN dotnet publish QDVapp/QDVapp.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app .

ENTRYPOINT ["dotnet", "QDVapp.dll"]