# Gamefilled V2 — preview do SITE COMPLETO

**Atualização 22/10/2026:** esta stack foi alterada para publicar o projeto principal
`Gamefilled.csproj` no host privado `https://preview.gamefilled.pt:8444`, e não
o micro-site técnico de `v2-modernization/Gamefilled.V2`.

## O que está incluído

- A homepage REAL do Gamefilled: capa de jogos, header, Trending, New and fresh, cartões, imagens e estilos originais.
- Páginas e rotas existentes de jogos, pesquisa e filtros, empresas, pessoas, perfis, biblioteca, registo/login e definições.
- Todas as alterações do projeto principal já integradas na `master`, incluindo as que ainda não foram publicadas no `dev.gamefilled.pt`.
- A base de dados **GamefilledFullPreview**, criada na SQL **da preview**. A base **Gamefilleddb** do `web-01` não é tocada.
- A base de dados `GamefilledV2Preview` dos anteriores testes do Identity permanece guardada noutro schema/database no mesmo volume SQL.

## O que ainda NÃO está incluído

O código moderno de Identity, confirmação de email, reset de palavra-passe e
nova biblioteca GUID continua em `v2-modernization/Gamefilled.V2`, mas ainda
não foi integrado no Gamefilled principal. **Não afirmar que essa segurança
está ativada no site completo.** A integração segura dos handlers, modelos
de sessão e relacionamentos de BD será efetuada no projeto principal sem
substituir as views e CSS originais. Ver `docs/V2_PRODUCT_INTEGRATION.md`.

## Inicializar uma base Identity vazia, sem migrar contas descartáveis

Após aprovação do PR, a criação da base Identity unificada é **manual**,
num passo independente do deploy do site:

```bash
cd /opt/gamefilled-preview/repo
git pull --ff-only origin master
bash preview/previewctl.sh check
bash preview/previewctl.sh init-identity-db
```

Confirma com `CREATE V2 IDENTITY DATABASE` (exatamente). O comando
cria/aplica as migrations EF para **`GamefilledV2Integrated`** dentro
do SQL Server de `docker-01`, reutilizando a instância SQL isolada,
sem importar a conta temporária ou quaisquer dados da base antiga.

**Isto NÃO substitui o site nem ativa Identity.** O `web` continua a
usar `GamefilledFullPreview`, com as contas descartáveis existentes.
A base antiga e os volumes permanecem intactos para rollback. Não uses
`docker compose down -v` nem elimines bases manualmente nesta fase.
A conta `sa` é utilizada apenas pelo container temporário das migrations,
nunca pelo site em execução. O `init-identity-db` é repetível (migrations
idempotentes) e não cria contas.

A ativação definitiva ficará dependente da matriz de funcionalidade em
`docs/V2_ACTIVE_FEATURE_PARITY.md`, com login, reviews, biblioteca, favoritos,
social, perfis e notificações testados no browser. É proibido permitir
simultaneamente Identity e Session para autorizar utilizadores.

## Separação de privilégios SQL (novo)

Antes, o container ASP.NET Core autenticava-se diretamente como `sa`
(SQL sysadmin). Isso permitia a uma eventual falha na aplicação ter
privilégios completos no SQL Server. Agora o container `web` usa
`gf_preview_app`, com **apenas db_datareader e db_datawriter na
`GamefilledFullPreview`**, sem `db_owner`, `db_ddladmin` ou
`sysadmin`. A conta administrativa continua a existir **somente** no
serviço SQL e nas tarefas one-shot de migration/provisioning.

### Migração manual obrigatória da configuração local

Na `docker-01`, **antes de fazer git pull/deploy**, abre o ficheiro
ignorado `/opt/gamefilled-preview/repo/preview/.env.preview` e acrescenta:

```text
PREVIEW_APP_SQL_PASSWORD=<UMA_PASSWORD_ALEATORIA_NOVA_DE_24_A_100_CARACTERES>
```

Para obter um valor forte **no próprio host**, gera-o localmente com
`openssl rand -base64 48` (não partilhes o resultado, nem o coloques no
GitHub ou nesta conversa). Mantém `PREVIEW_SQL_PASSWORD` anterior
**inalterado**: é a credencial da SQL existente e não deve ser rodada
sem processo próprio de rotação.

Na atualização, a sequência passa a:
1. Preflight exige os dois passwords **diferentes** e válidos.
2. Build do site principal, migrator e provisioner.
3. Aplica as migrations existentes só à `GamefilledFullPreview`
   com credenciais administrativas de setup.
4. Cria/revalida `gf_preview_app` e as permissões de leitura e escrita
   **apenas** nessa base; verifica que não pode alterar schema, criar
   tabelas ou executar operações como administrador.
5. Só então substitui `web` e valida o HTTPS e as páginas originais.

Se a preparação falhar, **não avança** para substituir o container web.
A password do runtime não é mostrada nos logs. Em caso de erro,
partilha **apenas a mensagem sem credenciais**, nunca `docker compose
config` com passwords expostas.

O projeto de configuração e CI usa a mesma conta e os mesmos limites,
mas testa numa instância SQL descartável, não no homelab.
**Não atualizar a web-01**. A migração futura do Identity terá a mesma
separação: conta de setup para migrations e conta de aplicação mínima.

## Atualizar a docker-01

Sem publicar DNS, abrir portas do router ou copiar segredos:

```bash
cd /opt/gamefilled-preview/repo
git pull --ff-only origin master
bash preview/previewctl.sh check
bash preview/previewctl.sh deploy
bash preview/previewctl.sh status
```

Quando a SQL existente estiver ativa, confirmar `UPDATE PREVIEW`.
O deploy constrói a imagem **principal** do Gamefilled e o seu EF bundle,
cria/aplica as migrations só à `GamefilledFullPreview` e, apenas
após sucesso, troca o container web da preview. Mantém os volumes SQL e
Caddy, incluindo o certificado já instalado no Windows. Nunca usar
`docker compose down -v` nem apontar ligações para a SQL do web-01.

## Auditoria de compatibilidade com ASP.NET Core Identity (sem alterações)

A cópia real da base de dados **ainda não começou**. Antes disso,
o novo comando permite medir incompatibilidades entre as contas e relações
antigas e a nova estrutura GUID:

```bash
cd /opt/gamefilled-preview/repo
bash preview/previewctl.sh audit
```

O comando compila e executa um container **só de leitura**, na rede SQL
interna, com as credenciais limitadas de `gf_preview_app`. Imprime
apenas números agregados: contas, biblioteca, favoritos, seguidores,
histórico e notificações, e contagens dos campos que ainda não podem ser
importados sem reconciliação. Não lista usernames, emails nem hashes
de passwords. **Não muda o site nem a base de dados.**

A lista de bloqueios e plano de migração sem perda de dados estão em
`docs/V2_LEGACY_DATA_MIGRATION.md`. A autenticação atual permanece
inalterada até a transição completa ser validada.

## Critérios de aceitação

Os testes automáticos devem validar o HTTPS, as rotas e os ficheiros CSS/imagens
originais e recusar uma homepage sem `gf-home-page`/`gf-home-hero`.
Depois confirmar manualmente imagens, pesquisa IGDB, game details, login,
registo, perfis, people, company, biblioteca e responsividade, lado a lado
com o `dev.gamefilled.pt`. A nova BD nasce vazia: criar contas de teste
descartáveis; não esperar ver as contas da BD de desenvolvimento.

**A preview completa só será considerada modernizada** quando o backend
Identity estiver integrado no site principal e todos os fluxos de segurança
e funcionalidades passarem os testes de regressão. Até lá é a cópia
completa do frontend e backend atual da branch principal, para podermos
avaliar mudanças visuais e de funcionalidades sem tocar no desenvolvimento.

Riscos de memória: a VM tem ~6 GiB RAM; monitorizar a SQL e a compilação.
Os ficheiros `.env.preview` e segredos locais nunca são publicados no Git.
