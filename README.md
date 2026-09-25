# FIT.AI — API de Treinos

![Node.js 24](https://img.shields.io/badge/Node.js-24-339933?style=for-the-badge&logo=nodedotjs)
![TypeScript](https://img.shields.io/badge/TypeScript-strict-3178C6?style=for-the-badge&logo=typescript)
![Fastify 5](https://img.shields.io/badge/Fastify-5-000000?style=for-the-badge&logo=fastify)
![Prisma 7](https://img.shields.io/badge/Prisma-7-2D3748?style=for-the-badge&logo=prisma)
![PostgreSQL 16](https://img.shields.io/badge/PostgreSQL-16-4169E1?style=for-the-badge&logo=postgresql)
![Zod 4](https://img.shields.io/badge/Zod-4-3E67B1?style=for-the-badge&logo=zod)
![Better-Auth](https://img.shields.io/badge/Auth-Better--Auth-black?style=for-the-badge)
![Vercel AI SDK 6](https://img.shields.io/badge/AI%20SDK-6-000000?style=for-the-badge&logo=vercel)

> **FIT.AI** é o nome que a pessoa lê — telas, logo, Figma. **`bootcamp-treinos-api`** é o nome do projeto: pacote, container do banco e título do OpenAPI.
>
> Este repositório é **só o back-end**. As telas ficam no front-end (`WEB_APP_BASE_URL`, por padrão `http://localhost:3000`).

## 📋 Introdução

Entre com o Google e converse com o **Coach AI**: ele pergunta **peso**, **altura**, **idade** e **% de gordura**, depois o **objetivo**, os **dias disponíveis** e as **restrições**, e monta um **plano de treino de 7 dias** — com divisão (split), exercícios, séries, repetições, descanso e imagem de capa. No dia a dia, a pessoa **inicia** e **conclui** o treino do dia, e a API calcula a **sequência** (🔥), a **consistência** por dia, os **treinos feitos**, a **taxa de conclusão** e o **tempo total**.

As telas atendidas são: **Login, AI Onboarding, Home, Chat da IA, Treino de Hoje, Plano de Treino, Dia do Plano, Evolução e Perfil.**

**Objetivo:** entregar ao front-end, por rotas REST tipadas, tudo o que as telas do FIT.AI precisam — com regra de negócio só nos use cases e validação só nas rotas.

> ⚠️ **Toda rota de dados é protegida.** A sessão vem do Better-Auth (`auth.api.getSession`) e toda consulta é filtrada pelo `userId` da sessão: a pessoa só vê e altera os próprios planos, dias e sessões. O Coach AI usa as mesmas regras — as tools recebem o `userId` por closure, nunca do modelo.

### Estado atual

O projeto foi construído em aulas, cada uma em uma branch. **`main` = `aula-03`**, que contém `aula-00` e `aula-01` (histórico linear, sem conflitos). As tarefas que deram origem às rotas estão em [`tasks/`](tasks/) (01 a 08, todas implementadas).

| Fase | Entrega | Estado |
|---|---|---|
| **aula-00** | Fundação: Fastify + Zod, Prisma + PostgreSQL, Better-Auth, plano de treino, sessões (iniciar/concluir), Home, Plano, Dia, Stats, `/me`, rotas da IA | Entregue |
| **aula-01** | Login com Google (sempre pede a conta), `todayWorkoutDay` opcional na Home, modelo OpenAI no chat | Entregue |
| **aula-03** | Variáveis de ambiente validadas com Zod, Dockerfile, pino-pretty, migrations do Prisma, cookies entre subdomínios em produção | Entregue |
| **Próxima** | Objetivo (`goal`) e capa (`coverImageUrl`) do plano; "Mudar objetivo" no chat; salvar o nome informado no onboarding | Pendente |
| **Depois** | Vídeo do exercício no chat; assinatura ("Plano Básico") | Pendente |

**O que falta para o Figma é pouco:** todas as telas já têm rota. As lacunas são o objetivo e a capa do **plano** (tela Plano de Treino e botão "Mudar objetivo") — o resto é formatação no front.

---

## 🏗️ Arquitetura do Sistema

Camadas em uma direção só: **Rotas → Use Cases → Prisma**. A rota valida (Zod) e autentica (Better-Auth), instancia um use case e traduz erros em status HTTP. O use case concentra a regra de negócio, fala direto com o Prisma e devolve um `OutputDto` — nunca o model do banco.

```mermaid
graph TD
    subgraph "Cliente"
        Web["Front-end FIT.AI<br/>(WEB_APP_BASE_URL)"]
    end

    subgraph "API (Fastify 5)"
        Rotas["src/routes<br/>(Zod, sessão,<br/>status HTTP)"]
        Auth["src/lib/auth.ts<br/>(Better-Auth,<br/>/api/auth/*)"]
        IA["src/routes/ai.ts<br/>(Coach AI, tools)"]
        UseCases["src/usecases<br/>(regras de negócio,<br/>InputDto → OutputDto)"]
        Prisma["src/lib/db.ts<br/>(Prisma + pg adapter)"]
    end

    Banco[("PostgreSQL 16")]
    Google["🌐 Google<br/>(OAuth)"]
    OpenAI["🌐 OpenAI<br/>(gpt-4o-mini)"]

    Web -->|"cookie de sessão"| Rotas
    Web -->|"login / logout"| Auth
    Web -->|"POST /ai (stream)"| IA
    Rotas --> Auth
    Rotas --> UseCases
    IA --> UseCases
    IA --> OpenAI
    Auth --> Google
    Auth --> Prisma
    UseCases --> Prisma
    Prisma --> Banco
```

---

## 🚀 Camadas do Projeto

**Rotas** (`src/routes/`)
: Handlers Fastify com `fastify-type-provider-zod`. Cada rota declara `tags`, `summary`, `params`/`querystring`/`body` e as respostas (incluindo `ErrorSchema`). Busca a sessão, chama **um** use case e trata os erros dele. Nenhuma regra de negócio.

**Use Cases** (`src/usecases/`)
: Uma classe por caso de uso, nomeada com verbo, com método `execute(dto: InputDto): Promise<OutputDto>`. Usam transações do Prisma quando precisam de atomicidade (ex.: desativar o plano ativo antes de criar o novo). Não tratam erro: lançam erros customizados.

**Schemas** (`src/schemas/index.ts`)
: Schemas Zod 4 compartilhados entre validação e OpenAPI. Dia da semana é sempre `z.enum(WeekDay)`, nunca `z.string()`.

**Erros** (`src/errors/index.ts`)
: `NotFoundError` (404), `WorkoutPlanNotActiveError` e `SessionAlreadyStartedError` (409).

**Autenticação** (`src/lib/auth.ts`)
: Better-Auth com adaptador Prisma e login social do Google (`prompt: "select_account"`). Rotas em `/api/auth/*`. Em produção, cookies valem para `.fullstackclub.com.br`.

**Coach AI** (`src/routes/ai.ts`)
: Vercel AI SDK 6 com `streamText` e até 10 passos de tool. Tools: `getUserTrainData`, `updateUserTrainData`, `getWorkoutPlans`, `createWorkoutPlan`. O system prompt define o tom, o onboarding, os splits por número de dias e as imagens de capa.

**Ambiente** (`src/lib/env.ts`)
: Variáveis validadas com Zod na subida. Se faltar alguma obrigatória, o servidor não sobe.

### Rotas

| Método | Rota | Tela | Use case |
|---|---|---|---|
| `GET` | `/home/:date` | Home | `GetHomeData` |
| `GET` | `/me` | Perfil | `GetUserTrainData` |
| `PUT` | `/me` | Onboarding / Perfil | `UpsertUserTrainData` |
| `GET` | `/stats?from=&to=` | Evolução | `GetStats` |
| `GET` | `/workout-plans?active=` | Plano de Treino | `ListWorkoutPlans` |
| `POST` | `/workout-plans` | (Coach AI) | `CreateWorkoutPlan` |
| `GET` | `/workout-plans/:workoutPlanId` | Plano de Treino | `GetWorkoutPlan` |
| `GET` | `/workout-plans/:workoutPlanId/days/:workoutDayId` | Treino de Hoje / Dia do Plano | `GetWorkoutDay` |
| `POST` | `/workout-plans/:workoutPlanId/days/:workoutDayId/sessions` | Iniciar Treino | `StartWorkoutSession` |
| `PATCH` | `/workout-plans/:workoutPlanId/days/:workoutDayId/sessions/:sessionId` | Marcar como concluído | `UpdateWorkoutSession` |
| `POST` | `/ai` | Chat / Onboarding | (tools acima) |
| `GET`/`POST` | `/api/auth/*` | Login / Sair da conta | Better-Auth |

### Modelo de Dados

Peso em **gramas** (`Int`); altura em **centímetros**; gordura corporal em **inteiro de 0 a 100**; duração e descanso em **segundos**. Datas com fuso (`Timestamptz`). Excluir um plano apaga dias, exercícios e sessões em cascata.

| Tabela | O que armazena |
|---|---|
| `user` | Nome, e-mail, foto, peso (g), altura (cm), idade, % de gordura |
| `WorkoutPlan` | Nome, dono, se é o plano ativo (só um ativo por vez) |
| `WorkoutDay` | Plano, nome, dia da semana (`WeekDay`), se é descanso, duração estimada (s), imagem de capa |
| `WorkoutExercise` | Dia, ordem, nome, séries, repetições, descanso (s) |
| `WorkoutSession` | Dia, início, conclusão (vazia = só iniciada) |
| `session` · `account` · `verification` | Tabelas do Better-Auth |

---

## 🎯 Sequência e consistência

A **sequência** (🔥) conta, a partir da data pedida para trás, os dias seguidos em que o plano ativo foi cumprido. **Dia de descanso conta** mesmo sem sessão. Dia da semana que não está no plano é pulado. A contagem para no primeiro dia de treino sem sessão concluída.

A **consistência** agrupa as sessões pela data de início (UTC): sessão concluída marca `workoutDayCompleted` e `workoutDayStarted`; sessão só iniciada marca apenas `workoutDayStarted`. Na **Home**, vem a semana inteira (domingo a sábado), inclusive dias sem sessão. Em **Evolução**, vêm só os dias com sessão dentro de `from`–`to`.

**Taxa de conclusão** = sessões concluídas ÷ total de sessões (0 se não houver). **Tempo total** = soma de (conclusão − início) das sessões concluídas, em segundos. Um dia só pode ser iniciado uma vez (409), e só em plano ativo (409).

---

## ⚙️ Como Executar

### 1. Pré-requisitos

| Componente | Requisito |
|---|---|
| **Node.js** | 24.x (`.nvmrc`; `engine-strict` ligado) |
| **pnpm** | 10.30.0 (`corepack enable`) |
| **Docker** | Para o PostgreSQL 16 (`docker-compose.yml`) |
| **Credenciais** | Google OAuth (client id e secret) e chave da OpenAI |

### 2. Configurar o ambiente

```sh
cp .env.example .env
```

| Variável | Uso |
|---|---|
| `PORT` | Porta da API (padrão `8080`) |
| `DATABASE_URL` | `postgresql://postgres:password@localhost:5432/bootcamp-treinos-api` com o compose local |
| `BETTER_AUTH_SECRET` | Segredo das sessões |
| `API_BASE_URL` | URL pública da API (padrão `http://localhost:8080`) |
| `GOOGLE_CLIENT_ID` · `GOOGLE_CLIENT_SECRET` | Login com Google |
| `OPENAI_API_KEY` | Coach AI (`gpt-4o-mini`) |
| `GOOGLE_GENERATIVE_AI_API_KEY` | Obrigatória na validação do ambiente, embora o chat use hoje a OpenAI |
| `WEB_APP_BASE_URL` | Origem do front (CORS e Better-Auth) |
| `NODE_ENV` | `development`, `production` ou `test` |

### 3. Banco e dependências

```sh
docker-compose up -d
pnpm install
pnpm exec prisma migrate dev
```

### 4. Rodar

```sh
pnpm dev
```

A documentação interativa fica em **`/docs`** (Scalar), com a API e as rotas de autenticação. O OpenAPI puro está em **`/swagger.json`**.

### 5. Qualidade

```sh
pnpm exec eslint .
pnpm exec prettier --write .
```

> ⚠️ **Ainda não há testes automatizados.** As regras de sequência, consistência e estatísticas estão nos use cases e são as primeiras candidatas a teste.

### 6. Build e Docker

```sh
pnpm build            # prisma generate + tsc → dist/
docker build -t bootcamp-treinos-api .
```

> ⚠️ **A imagem não roda migrations.** Aplique `pnpm exec prisma migrate deploy` no banco de produção antes de subir uma versão com migration nova.

---

## 📂 Estrutura de Pastas

- **`CLAUDE.md`** — orientação do projeto para o Claude Code
- **`README.md`** — este arquivo
- **`.claude/rules/`** — regras de arquitetura, TypeScript e gerais (rotas, use cases, commits)
- **`docs/API_PROMPT.md`** — template de prompt para criar uma rota
- **`tasks/`** — as tarefas 01 a 08 que deram origem às rotas
- **`prisma/`** — `schema.prisma` e `migrations/`
- **`src/`** — o código

```
bootcamp-treinos-FSC/
├── CLAUDE.md · README.md · Dockerfile · docker-compose.yml · .env.example
├── .claude/rules/
├── docs/API_PROMPT.md
├── tasks/                     (01.md … 08.md)
├── prisma/
│   ├── schema.prisma
│   └── migrations/
└── src/
    ├── index.ts               (Fastify, CORS, Swagger, Scalar, registro das rotas)
    ├── lib/                   (auth.ts · db.ts · env.ts)
    ├── routes/                (ai · home · me · stats · workout-plan)
    ├── usecases/              (10 casos de uso)
    ├── schemas/index.ts
    ├── errors/index.ts
    └── generated/prisma/      (FORA DO GIT — gerado pelo Prisma)
```

---

## 📚 Documentação

- [`CLAUDE.md`](CLAUDE.md): visão geral, comandos e convenções
- [`.claude/rules/architecture.md`](.claude/rules/architecture.md): como escrever rotas e use cases, com exemplos
- [`.claude/rules/general.md`](.claude/rules/general.md): stack e estrutura
- [`.claude/rules/typescript.md`](.claude/rules/typescript.md): convenções de TypeScript
- [`docs/API_PROMPT.md`](docs/API_PROMPT.md): template para pedir uma rota nova
- [`tasks/`](tasks/): especificação de cada rota entregue
- [`prisma/schema.prisma`](prisma/schema.prisma): o modelo de dados
- `/docs` (com o servidor rodando): referência interativa da API

---

## 📄 Licença e Uso

Projeto desenvolvido no bootcamp do Full Stack Club. O `package.json` declara a licença **ISC**; nenhum arquivo `LICENSE` foi adicionado ainda.
