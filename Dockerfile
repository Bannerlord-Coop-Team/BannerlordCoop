FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine

# Game assemblies copied in by DockerPrepare.ps1. CI workflows set the same BANNERLORD_DIR, so keep the two in step.
ENV BANNERLORD_DIR=/home/mb2
WORKDIR /home
COPY DockerAssembliesTemp /home/mb2

# Run ".\DockerPrepare.ps1" first (needs BANNERLORD_DIR set by runmefirst.cmd)
# Run "docker build -t garrettluskey/bannerlordcoop:latest -t garrettluskey/bannerlordcoop:<version> ."
# "docker push garrettluskey/bannerlordcoop:<version>"
# "docker push garrettluskey/bannerlordcoop:latest"
