FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine

# Links the GHCR package to this repo so it inherits the repo's access.
LABEL org.opencontainers.image.source=https://github.com/Bannerlord-Coop-Team/BannerlordCoop

# Game assemblies copied in by DockerPrepare.ps1. CI workflows set the same BANNERLORD_DIR, so keep the two in step.
ENV BANNERLORD_DIR=/home/mb2
WORKDIR /home
COPY DockerAssembliesTemp /home/mb2

# Run ".\DockerPrepare.ps1" first (needs BANNERLORD_DIR set by runmefirst.cmd)
# Run "docker login ghcr.io" with a classic token that has write:packages
# Run "docker build -t ghcr.io/bannerlord-coop-team/bannerlordcoop:latest -t ghcr.io/bannerlord-coop-team/bannerlordcoop:v<game version> ."
# "docker push ghcr.io/bannerlord-coop-team/bannerlordcoop:v<game version>"
# "docker push ghcr.io/bannerlord-coop-team/bannerlordcoop:latest"
