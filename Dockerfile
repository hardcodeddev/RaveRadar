# Stage 1: Build the React frontend
FROM node:22-alpine AS client-build
WORKDIR /src/RaveRadar.Client
COPY RaveRadar.Client/package*.json ./
RUN npm install
COPY RaveRadar.Client/ ./
RUN npm run build

# Stage 2: Build the ASP.NET Core backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS server-build
WORKDIR /src
COPY RaveRadar.sln .
COPY RaveRadar.Api/*.csproj ./RaveRadar.Api/
RUN dotnet restore
COPY RaveRadar.Api/ ./RaveRadar.Api/
# Copy the dataset file from the root to the API project directory so it's included
COPY edm_artists_dataset.txt ./RaveRadar.Api/
# Copy frontend build to backend's wwwroot so it can serve it
COPY --from=client-build /src/RaveRadar.Client/dist/ ./RaveRadar.Api/wwwroot/
RUN dotnet publish RaveRadar.Api/RaveRadar.Api.csproj -c Release -o /app/out

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=server-build /app/out .

# Ensure the data folder for SQLite exists and is writable
# (important for Render persistent disk mounts)
RUN mkdir -p /app/data && chmod 777 /app/data

# Install Python + supervisor for the ML sidecar
RUN apt-get update && apt-get install -y python3 python3-pip supervisor && rm -rf /var/lib/apt/lists/*
COPY recommendation-engine/requirements.txt /app/engine/requirements.txt
RUN pip3 install --no-cache-dir --break-system-packages -r /app/engine/requirements.txt
COPY recommendation-engine/ /app/engine/
COPY supervisord.conf /etc/supervisord.conf

# Default environment variables
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/RaveRadar.db"
ENV ASPNETCORE_ENVIRONMENT=Production

# Port exposure (Render injects $PORT at runtime, defaults to 10000)
EXPOSE ${PORT:-10000}

# Run both dotnet API and Python ML engine via supervisord
# ASPNETCORE_URLS is set dynamically so the app binds to whatever port Render assigns via $PORT
ENTRYPOINT ["/bin/sh", "-c", "export ASPNETCORE_URLS=\"http://+:${PORT:-8080}\" && exec /usr/bin/supervisord -c /etc/supervisord.conf"]
