# Construcheck

SaaS de gestão de obras — Implementação de um sistema cobrindo orçamentos, notas de pagamento, custos e agendamentos de obras.

## Sumário

- [Sobre o projeto](#sobre-o-projeto)
- [Desafios (Code Review)](#desafios-code-review)
- [Stack](#stack)
- [Arquitetura](#arquitetura)
- [Pré-requisitos](#pré-requisitos)
- [Instalação e execução](#instalação-e-execução)
  - [Rodando com Docker (recomendado)](#rodando-com-docker-recomendado)
  - [Rodando localmente (sem Docker)](#rodando-localmente-sem-docker)
- [Rotas da API](#rotas-da-api)
  - [Autenticação](#autenticação)
  - [Health Check](#health-check)
- [Autenticação e autorização](#autenticação-e-autorização)
- [Testes](#testes)
- [Estrutura do projeto](#estrutura-do-projeto)
- [CI/CD](#cicd)

## Sobre o projeto

O Construcheck é uma plataforma para gestão de obras, permitindo controlar orçamentos, notas de pagamento, custos e agendamentos relacionados a uma construção. O backend e o frontend são publicados juntos em uma única imagem Docker, o que simplifica o deploy: o .NET serve tanto a API quanto os arquivos estáticos do Angular a partir da mesma aplicação.

Atualmente o módulo de autenticação (registro, login, controle de acesso por papéis) está implementado e testado. Os módulos de domínio relacionados a obras (orçamentos, custos, notas, agendamentos) estão em desenvolvimento.

O Objetivo deste projeto é mostrar experiência prática em Deploy automatico no Azure, através de CI/CD e github actions, Utilizando praticas de desenvolvimento em produção, com maximo cuidado para não expor variaveis de ambiente e secrets, observabilidade com logs para acelerar no processo de debug.

## Desafios (Code Review)

1. Encontre qualquer variavel de ambiente exposta
2. Busque vulnerabilidades
3. Problemas de performance gerados pelo código

## Stack

| Camada | Tecnologia |
|---|---|
| Frontend | Angular 19 (standalone) + Tailwind CSS |
| Backend | .NET 10 |
| Banco de dados | SQL Server |
| Containerização | Docker (imagem única — front + back) |
| Cloud | Azure Container Apps |
| Pipeline | GitHub Actions |

## Arquitetura

O backend segue uma arquitetura **Modulith** (monólito modular): cada módulo de domínio é um projeto `.csproj` separado, organizado para que possa, no futuro, ser extraído como microserviço caso necessário. A exceção é o módulo `Auth`, que é tratado como infraestrutura da própria API e não como um candidato a microserviço — em um cenário futuro de microserviços, ele seria usado por um API Gateway para validar acessos.

```
backend/
  src/
    API/                 # entry point da aplicação; referencia Core e SharedKernel
      Controllers/
      Data/               # AppDbContext
      Extensions/
      Middleware/         # GlobalExceptionHandler
      Modules/
        Auth/             # infraestrutura da API (não é candidato a microserviço)
      Program.cs
    Core/                 # domínio de negócio, candidato a microserviço
    SharedKernel/         # primitivos transversais, sem dependência de ASP.NET
  tests/
    Unit/
    Integration/
  dockerfile
  docker-compose.yml
frontend/
  src/
    app/
      core/               # guards, interceptors, services
      shared/              # componentes e layouts
      features/
        auth/
        dashboard/
```

No frontend, a estrutura é feature-driven, separando código central (`core`), componentes compartilhados (`shared`) e funcionalidades (`features`).

Em produção, o Angular é compilado e o resultado copiado para `API/wwwroot` durante o build multi-stage do `dockerfile`. O próprio .NET serve esses arquivos estáticos, então frontend e backend ficam same-origin — não há necessidade de configurar CORS fora do ambiente de desenvolvimento.

## Pré-requisitos

Para rodar com Docker (recomendado):

- [Docker](https://www.docker.com/) e Docker Compose

Para rodar localmente sem Docker:

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Node.js](https://nodejs.org/) e [Angular CLI](https://angular.dev/tools/cli) (`npm install -g @angular/cli`)
- SQL Server (local ou em container)
- Ferramenta `dotnet-ef` instalada globalmente: `dotnet tool install --global dotnet-ef`

## Instalação e execução

### Rodando com Docker (recomendado)

1. Clone o repositório:

   ```bash
   git clone https://github.com/shimworks/Construcheck.git
   cd construcheck
   ```

2. Copie o arquivo de exemplo de variáveis de ambiente e ajuste os valores conforme necessário:

   ```bash
   cd backend
   cp .env.example .env
   ```

3. Suba os containers
  Existem 2 arquivos docker compose, o atual é uma simulação de como está em produção (deploy em um unico container) se quiser os containers separados renomeie o docker-compose-dev.yml para docker-compose.yml 
   ```bash
   docker compose up --build -d
   ```

4. A API estará disponível em `http://localhost:8080`. As migrations do banco são aplicadas automaticamente na inicialização (com retry de 5 tentativas a cada 5 segundos, caso o banco ainda esteja subindo).

> O `.env` nunca deve ser commitado — ele já está incluído no `.gitignore`. Use `.env.example` como referência de quais variáveis preencher.

### Rodando localmente (sem Docker)

**Backend:**

1. Dentro de `backend/src/API`, configure os segredos necessários via `secrets.json` (não use `.env` neste modo — ele é usado apenas pelo fluxo Docker):

  ```bash
    dotnet user-secrets init
    dotnet user-secrets set "ASPNETCORE_ENVIRONMENT" "Development"
    dotnet user-secrets set "DB_SERVER" "localhost"
    dotnet user-secrets set "DB_PORT" "1433"
    dotnet user-secrets set "DB_NAME" "construcheck_db"
    dotnet user-secrets set "DB_USER" "ADMIN"
    dotnet user-secrets set "DB_PASSWORD" "DB_PASSWORD"
    dotnet user-secrets set "JWT_SECRET" "JWT_SECRET"
    dotnet user-secrets set "JWT_ISSUER" "JWT_ISSUER"
    dotnet user-secrets set "JWT_AUDIENCE" "JWT_AUDIENCE"
    dotnet user-secrets set "JWT_EXPIRATION_MINUTES" "60"
    dotnet user-secrets set "REFRESH_TOKEN_EXPIRATION_DAYS" "7"
    dotnet user-secrets set "ASPNETCORE_URLS" "http://+:8080"
  ```

   Consulte a seção [Variáveis de ambiente](#variáveis-de-ambiente) para a lista completa de chaves esperadas.

2. Aplique as migrations:

   ```bash
   dotnet ef database update --project src/API
   ```

3. Rode a API:

   ```bash
   dotnet run --project src/API
   ```

**Frontend:**

1. Dentro da pasta `frontend/`, instale as dependências:

   ```bash
   npm install
   ```

2. Rode o servidor de desenvolvimento:

   ```bash
   ng serve
   ```

3. O frontend ficará disponível em `http://localhost:4200`, consumindo a API conforme o `apiUrl` configurado em `environments/`.

> Em desenvolvimento, o frontend (`ng serve`, porta 4200) e o backend rodam em portas separadas, por isso o CORS está habilitado para `http://localhost:4200` nesse cenário. Em produção, ambos rodam na mesma origem e o CORS não é necessário.

## Rotas da API

Todas as rotas abaixo estão sob o prefixo `/api`. A documentação interativa (Swagger) fica disponível ao rodar a API localmente, em `/swagger`.

### Autenticação

Rotas do módulo `Auth`, responsável por registro, login e controle de acesso.

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| `POST` | `/api/auth/register` | Público | Registra um novo usuário. Retorna `201 Created`, sem token — o login é feito separadamente. O usuário recebe o papel padrão `Viewer`. |
| `POST` | `/api/auth/login` | Público | Autentica o usuário. Retorna o `accessToken` no corpo da resposta e define um cookie `HttpOnly` com o refresh token. |
| `POST` | `/api/auth/refresh` | Público (via cookie) | Renova o access token usando o refresh token armazenado no cookie. O refresh token antigo é revogado e um novo é emitido (rotação). |
| `POST` | `/api/auth/logout` | Autenticado | Revoga o refresh token atual. Retorna `204 No Content`. |
| `PUT` | `/api/auth/users/{id}/roles` | Admin | Atualiza os papéis (roles) de um usuário. Recebe uma lista de papéis (`RoleType`). |

**Exemplo — registro:**

```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "usuario@exemplo.com",
  "password": "senha-segura"
}
```

**Exemplo — login:**

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "usuario@exemplo.com",
  "password": "senha-segura"
}
```

Resposta:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs..."
}
```

(o refresh token é enviado automaticamente via cookie `HttpOnly`, não aparece no corpo da resposta)

**Exemplo — atualizar papéis de um usuário (requer Admin):**
0 = Admin
1 = Viewer (Usuario Padrão)
```http
PUT /api/auth/users/{id}/roles
Authorization: Bearer <access-token>
Content-Type: application/json

{
  "roles": [0, 1]
}
```

### Health Check

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| `GET` | `/api/health` | Público | Verifica se a API está no ar. |

## Autenticação e autorização

- O acesso é controlado por **RBAC** (Role-Based Access Control), com os papéis `Admin` e `Viewer` criados por padrão (seed) na migration inicial.
- Usuários com papel `Viewer` têm acesso apenas de visualização; usuários `Admin` têm acesso completo, incluindo a promoção de outros usuários.
- O **access token** é um JWT retornado no corpo da resposta de login/refresh e deve ser enviado no header `Authorization: Bearer <token>` nas rotas protegidas.
- O **refresh token** é armazenado em um cookie `HttpOnly`, o que evita exposição via JavaScript no frontend. A cada uso, ele é rotacionado: o token usado é revogado e um novo é emitido.
- O sistema não implementa múltiplas sessões simultâneas por usuário — cada login gera um refresh token independente.

## Testes

O projeto possui testes unitários e de integração para o módulo de autenticação.

```bash
# Rodar todos os testes
dotnet test

# Rodar apenas os testes unitários
dotnet test tests/Unit/Construcheck.Unit.Tests.csproj

# Rodar apenas os testes de integração
dotnet test tests/Integration/Construcheck.Integration.Tests.csproj
```

- **Testes unitários** (`xUnit` + `NSubstitute`): cobrem `AuthService` e `TokenService`, mockando as dependências via interfaces (`IAuthRepository`, `ITokenService`).
- **Testes de integração** (`xUnit` + `WebApplicationFactory`): fazem requisições HTTP reais contra a API, usando um banco em memória (EF Core InMemory) no lugar do SQL Server, isolado por execução.

## Estrutura do projeto

```
backend/
  src/
    API/
      Controllers/
      Data/                      # AppDbContext
      Extensions/                # ResultExtensions
      Middleware/                # GlobalExceptionHandler
      Modules/
        Auth/
          Controllers/
          DTOs/
          Entities/
          Enums/
          Interfaces/
          Repositories/
          Services/
          AuthModule.cs
      Program.cs
      API.csproj
    Core/                        # domínio de negócio (obras, orçamentos, etc.)
      Core.csproj
    SharedKernel/                # Result<T> e primitivos transversais
      Result.cs
      SharedKernel.csproj
  tests/
    Unit/
      Auth/
        Services/
    Integration/
      Auth/
      Infrastructure/
  dockerfile
  docker-compose.yml
  docker-compose.override.yml
  .env.example
  construcheck.slnx
frontend/
  src/
    app/
      core/
      shared/
      features/
        auth/
        dashboard/
    environments/
```

## CI/CD

O pipeline (`.github/workflows/ci-cd.yml`) roda em dois jobs:

1. **CI** — restore, build e execução dos testes a cada push.
2. **CD** — disparado apenas em push na branch `main`: constrói a imagem Docker, publica no Azure Container Registry e faz o deploy no Azure Container Apps.

O fluxo de branches utilizado é `develop` → `main`, sendo que o deploy automático ocorre exclusivamente a partir da `main`. As variáveis de ambiente de produção são gerenciadas pelo pipeline
