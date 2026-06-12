FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine

WORKDIR /home
COPY DockerAssembliesTemp ./mb2

# Run "docker build -t garrettluskey/bannerlordcoop:latest -t garrettluskey/bannerlordcoop:<version> ."
# "docker push garrettluskey/bannerlordcoop:<version>"
# "docker push garrettluskey/bannerlordcoop:latest"
