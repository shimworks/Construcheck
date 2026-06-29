# === STAGE 1: Build do Frontend Angular ===
FROM node:22-alpine AS front-build
WORKDIR /app-front
# Copia arquivos de dependências baseados na raiz do repositório
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
# Compila o Angular gerando os arquivos de produção
RUN npm run build -- --configuration=production

# === STAGE 2: Build do Backend .NET 10 ===
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS back-build
WORKDIR /app-back
# Copia a solução e os projetos para restaurar as dependências
COPY backend/construcheck.slnx ./
COPY backend/src/API/API.csproj backend/src/API/
COPY backend/src/Core/Core.csproj backend/src/Core/
COPY backend/src/SharedKernel/SharedKernel.csproj backend/src/SharedKernel/
RUN dotnet restore backend/construcheck.slnx

# Copia o resto do código fonte do backend e publica
COPY backend/src/ backend/src/
RUN dotnet publish backend/src/API/API.csproj -c Release -o /app/publish

# === STAGE 3: Runtime Final ===
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Copia o binário do .NET compilado no Stage 2
COPY --from=back-build /app/publish .

# Copia o frontend compilado do Stage 1 direto para a pasta wwwroot do .NET
# NOTA: Verifique se no angular.json a pasta de saída é exatamente dist/construcheck/browser
COPY --from=front-build /app-front/dist/construcheck/browser ./wwwroot

ENTRYPOINT ["dotnet", "API.dll"]