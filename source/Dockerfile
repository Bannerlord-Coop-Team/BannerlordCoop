# https://hub.docker.com/_/microsoft-dotnet
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /source

# copy csproj and restore as distinct layers
COPY *.sln .
COPY source/*/*.csproj ./source/
RUN dotnet restore

# copy everything else and build app
COPY source/. ./source/
WORKDIR /source/
RUN dotnet publish -c release -o /app --no-restore

# final stage/image
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet"]