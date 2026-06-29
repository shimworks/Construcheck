# Construcheck Frontend

Frontend Angular 19 do projeto Construcheck — MVP de autenticação (login, registro, refresh, logout).

## Pré-requisitos

- Node.js 20+
- npm 10+
- Backend Construcheck rodando (padrão: `http://localhost:5151`)

## Instalação

```bash
cd frontend
npm install
```

## Desenvolvimento

```bash
npm start
```

A aplicação estará em `http://localhost:4200`.

O proxy em `proxy.conf.json` encaminha `/api/*` para `http://localhost:5151`, evitando problemas de CORS em desenvolvimento local.

## Build de produção

```bash
npm run build
```

Artefatos em `dist/construcheck/`. A URL da API em produção está em `src/environments/environment.prod.ts`.

## Rotas

| Rota | Descrição |
|------|-----------|
| `/auth/login` | Login |
| `/auth/register` | Cadastro |
| `/dashboard` | Dashboard protegida (requer autenticação) |

## Integração com API

| Endpoint | Uso |
|----------|-----|
| `POST /api/auth/register` | Cadastro |
| `POST /api/auth/login` | Login (retorna `accessToken`, cookie `refreshToken`) |
| `POST /api/auth/refresh` | Renovação de sessão via cookie |
| `POST /api/auth/logout` | Encerramento de sessão |
| `GET /api/health` | Health check na dashboard |

## Arquitetura

- **Standalone components** (sem NgModules)
- **Signals** para estado de autenticação
- **Functional guards** e **interceptors**
- Estrutura **feature-driven**: `core/`, `shared/`, `features/auth/`, `features/dashboard/`

Consulte `instructions.md` para detalhes completos da especificação.
