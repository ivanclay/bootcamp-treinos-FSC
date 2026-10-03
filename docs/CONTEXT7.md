# Context7 (documentação atualizada para o Claude)

O [Context7](https://github.com/upstash/context7) é um servidor MCP que entrega ao Claude a documentação **atual** das bibliotecas do projeto (ASP.NET Core, EF Core, .NET MAUI, CommunityToolkit.Mvvm, Microsoft.Extensions.AI, Npgsql, xUnit...). Assim o Claude erra menos em APIs novas ou que mudaram.

A regra em `.claude/rules/general.md` ("SEMPRE use Context7 para buscar documentações") só tem efeito se o Context7 estiver instalado. Não é necessário para compilar ou rodar o projeto.

## O que precisa ser feito (uma vez)

### Claude Code no seu computador

1. No terminal:

   ```bash
   claude mcp add --transport http context7 https://mcp.context7.com/mcp
   ```

2. Confira se aparece na lista:

   ```bash
   claude mcp list
   ```

3. Abra uma nova sessão do Claude Code. Se o comando acima não funcionar, siga o README do Context7 (o endereço pode mudar).

### Claude Code na nuvem (claude.ai/code)

1. Conecte o Context7 em <https://claude.ai/customize/connectors>.
2. Inicie uma **nova** sessão (os conectores são carregados quando a sessão começa).

## Depois de instalado

Nada a fazer: o Claude lê a regra do projeto e consulta o Context7 sozinho. Para forçar, escreva "use context7" no pedido.
