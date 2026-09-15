FROM mcr.microsoft.com/dotnet/sdk:8.0 AS net8
FROM mcr.microsoft.com/dotnet/sdk:10.0
COPY --from=net8 /usr/share/dotnet/shared/Microsoft.NETCore.App /usr/share/dotnet/shared/Microsoft.NETCore.App
COPY --from=net8 /usr/share/dotnet/shared/Microsoft.AspNetCore.App /usr/share/dotnet/shared/Microsoft.AspNetCore.App
RUN apt-get update && apt-get install -y --no-install-recommends clang zlib1g-dev curl && rm -rf /var/lib/apt/lists/*
WORKDIR /work
