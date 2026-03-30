FROM mcr.microsoft.com/dotnet/sdk:10.0.201-alpine3.23-amd64

WORKDIR /home
COPY DockerAssembliesTemp ./mb2

# Run "docker build -t garrettluskey/bannerlordcoop:latest -t garrettluskey/bannerlordcoop:<version> ."
# "docker push garrettluskey/bannerlordcoop:<version>"
# "docker push garrettluskey/bannerlordcoop:latest"W