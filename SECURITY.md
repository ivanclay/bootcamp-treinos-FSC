# Segurança

Revisão feita em 25/09/2026 sobre a versão .NET. Este arquivo registra o que já está protegido, o que foi corrigido e o que ainda falta antes de produção.

## Proteções existentes

- **Isolamento de dados:** toda consulta é filtrada pelo usuário autenticado; o professor só acessa os próprios alunos (`StudentAccess`). Fora do escopo a API responde 404, sem revelar se o recurso existe.
- **Papel e bloqueio lidos do banco** a cada requisição (`ICurrentUser`): promover, rebaixar ou bloquear vale na hora, mesmo com um JWT ainda válido.
- **Login:** Google OAuth feito pela API → código de uso único (2 min, salvo só como hash SHA-256, uso único garantido no banco) → JWT. Redirects só para `Auth:AllowedRedirectUris`.
- **PKCE (S256) obrigatório** na troca do código: um código interceptado ou injetado não serve sem o verifier de quem iniciou o login.
- **Web:** antiforgery em todos os POST, cookie de sessão HttpOnly, HTML bruto desabilitado nas respostas do Coach e links só http/https/mailto.
- **Coach AI:** as tools sempre agem sobre o usuário autenticado; o modelo não escolhe de quem são os dados. Histórico enviado ao modelo limitado às últimas 30 mensagens.
- **Banco:** apenas EF Core (consultas parametrizadas).
- **Dependências:** `dotnet list package --vulnerable --include-transitive` sem pacotes vulneráveis.

## Corrigido nesta revisão

| # | Gravidade | Problema | Correção |
| --- | --- | --- | --- |
| 1 | Alta | `docker-compose` roda em Development (login só com e-mail, usuários de exemplo, segredo JWT público) e expunha as portas na rede | Portas presas em `127.0.0.1`; a API **não sobe** fora de Development com o segredo JWT de desenvolvimento; dados de exemplo e `/docs` só em Development (ou `Api:ExposeDocs=true`) |
| 2 | Média | XSS por link `javascript:` na resposta do Coach AI (a IA pode ser induzida por prompt injection) | Links do markdown filtrados por lista de esquemas permitidos; imagens viram texto (`Markdown.cs`, testes em `tests/FitAi.Web.Tests`) |
| 3 | Média | Professor vinculava sem consentimento um aluno já cadastrado só informando o e-mail | Aluno existente recebe o convite no Perfil e precisa **aceitar** (pode recusar); só cadastros novos que entram pelo convite são vinculados automaticamente |
| 4 | Média | Sem limite de requisições (custo do Coach AI, força bruta em códigos de convite) | Rate limiting: login 60/min por IP (e 20/min por cliente na Web), Coach 10/min e 200/dia por usuário, código de convite 5 a cada 10 min, teto geral 300/min; resposta 429 `RATE_LIMITED` com `Retry-After` |
| 5 | Média | App Android: HTTP liberado em Release e código do deep link `fitai://` interceptável por outro app | HTTP só em Debug; `allowBackup=false`; PKCE no login |
| 6 | Baixa | Login CSRF (fazer a vítima entrar na conta do atacante) | Resolvido pelo PKCE: o verifier fica num cookie do navegador que iniciou o login |
| 7 | Baixa | `X-Forwarded-*` aceitos de qualquer cliente (IP e esquema forjáveis, inclusive para burlar o rate limit) | Só são usados com `ReverseProxy:TrustForwardedHeaders=true` (atrás de proxy); `/docs` fechado em produção |

Os limites ficam na seção `RateLimit` do `appsettings.json` da API.

## Pagamentos

- **Nenhum dado de cartão** passa pelo servidor: PIX pelo QR do Asaas; cartão e boleto na fatura hospedada do Asaas.
- **Preço definido no servidor** (`Plans:Pro`), nunca pelo formulário; CPF/CNPJ validado pelos dígitos verificadores e nunca registrado em log (só método, caminho e status das chamadas ao Asaas).
- **Webhook** `POST /webhooks/asaas`: anônimo por natureza, autenticado pelo token `asaas-access-token` comparado em tempo constante (token vazio rejeita tudo), corpo limitado a 64 KB, idempotência atômica (`INSERT ... ON CONFLICT DO NOTHING` antes de qualquer efeito, liberada se falhar) e 500 em falha para o Asaas reenviar.
- **Segredos** (`Asaas__ApiKey`, `Asaas__WebhookToken`) só por variável de ambiente/cofre. Chaves de sandbox e de produção são diferentes: não misture.
- **Simulação de pagamento** só existe com `Payments:Provider=Fake` **e** em Development.
- Pendente: o corpo do webhook é aceito como verdade quando o token confere. Para defesa extra contra vazamento do token, reconsulte a cobrança no Asaas (`GET payments/{id}`) antes de aplicar.

## Pendências conhecidas

- **Imagens de capa externas:** professores e a IA podem cadastrar URLs de qualquer site; o navegador do aluno carrega essas imagens (o site externo vê o acesso). Considere restringir a `/covers/...` ou a uma lista de domínios.
- **Revogação de sessão:** o JWT vale 7 dias; sair da conta apaga o token no cliente, mas não o invalida. Para revogar antes, bloqueie o usuário. Um refresh token com expiração curta resolveria.
- **HTTPS:** a API e a Web não forçam HTTPS por conta própria — em produção, publique atrás de um proxy/ingress com TLS e ligue `ReverseProxy:TrustForwardedHeaders`.
- **Chaves de proteção de dados da Web:** em containers, as chaves do cookie de login ficam dentro do container; ao recriá-lo, todos precisam entrar de novo. Em produção, persista-as (volume ou `PersistKeysTo...`).

## Checklist de produção

- [ ] `ASPNETCORE_ENVIRONMENT=Production` na API e na Web.
- [ ] `Auth:JwtSecret` próprio, aleatório, com 32+ caracteres (a API recusa o de desenvolvimento).
- [ ] `Auth:Google:ClientId/ClientSecret` configurados; `Auth:AllowedRedirectUris` só com as URLs reais (HTTPS e `fitai://auth`).
- [ ] `Auth:AdminEmails` com os e-mails dos administradores.
- [ ] Chaves (OpenAI, Google, YouTube) em variáveis de ambiente ou cofre de segredos.
- [ ] TLS no proxy/ingress e `ReverseProxy:TrustForwardedHeaders=true`.
- [ ] App: `AppConfig.ApiBaseUrl` de Release apontando para a URL HTTPS da API.
- [ ] Revisar os limites de `RateLimit` conforme o uso real.
- [ ] Pagamentos: `Payments__Provider=Asaas`, chave de **produção** em `Asaas__ApiKey`, `Asaas__BaseUrl=https://api.asaas.com/v3/`, `Asaas__WebhookToken` forte e o mesmo cadastrado no painel; verificação no sandbox concluída (README).

## Como reportar

Encontrou um problema de segurança? Abra uma issue privada (Security advisory) no GitHub em vez de uma issue pública.
