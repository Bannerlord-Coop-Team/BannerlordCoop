ARG REPO=mcr.microsoft.com/dotnet/aspnet
FROM $REPO:6.0.16-alpine3.17-amd64

# Unset ASPNETCORE_URLS from aspnet base image
ENV ASPNETCORE_URLS=
# Do not generate certificate
ENV DOTNET_GENERATE_ASPNET_CERTIFICATE=false
# Do not show first run text
ENV DOTNET_NOLOGO=true
# SDK version
ENV DOTNET_SDK_VERSION=6.0.408
# Disable the invariant mode (set in base image)
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
# Enable correct mode for dotnet watch (only mode supported in a container)
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
# Skip extraction of XML docs - generally not useful within an image/container - helps performance
ENV NUGET_XMLDOC_MODE=skip
# PowerShell telemetry for docker image usage
ENV POWERSHELL_DISTRIBUTION_CHANNEL=PSDocker-DotnetSDK-Alpine-3.17

RUN apk add --no-cache \
        curl \
        icu-data-full \
        icu-libs \
        git

# Install .NET SDK
RUN wget -O dotnet.tar.gz https://dotnetcli.azureedge.net/dotnet/Sdk/$DOTNET_SDK_VERSION/dotnet-sdk-$DOTNET_SDK_VERSION-linux-musl-x64.tar.gz \
    && dotnet_sha512='241f1ef5c32a277bed881443de2ff17ceeba100f7191c4929108b65fde42d267aa4ab53f45fde728009185d4b5ac061d1e276d14e56b964d1b3104db0608fafd' \
    && echo "$dotnet_sha512  dotnet.tar.gz" | sha512sum -c - \
    && mkdir -p /usr/share/dotnet \
    && tar -oxzf dotnet.tar.gz -C /usr/share/dotnet ./packs ./sdk ./sdk-manifests ./templates ./LICENSE.txt ./ThirdPartyNotices.txt \
    && rm dotnet.tar.gz \
    # Trigger first run experience by running arbitrary cmd
    && dotnet help

# Copy MB assemblies (Make sure you run DockerPrepare.ps1 first)
WORKDIR /home
COPY DockerAssembliesTemp ./mb2

# Run "docker build -t garrettluskey/bannerlordcoop:latest -t garrettluskey/bannerlordcoop:<version> ."
# "docker push garrettluskey/bannerlordcoop:<version>"
# "docker push garrettluskey/bannerlordcoop:latest"