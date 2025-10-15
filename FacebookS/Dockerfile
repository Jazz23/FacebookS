# Use official Microsoft .NET SDK image to build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /app

# Copy project files
COPY . .

# Restore dependencies
RUN dotnet restore

# Build the project in Release mode
RUN dotnet publish -c Release --self-contained true -o /out


# ---- RUNTIME IMAGE ----
FROM mcr.microsoft.com/playwright/dotnet:v1.55.0-noble AS runtime

WORKDIR /app

# Copy published app from build image
COPY --from=build /out ./

RUN chmod +x FacebookS

# Set the entry point
ENTRYPOINT ["./FacebookS"]
