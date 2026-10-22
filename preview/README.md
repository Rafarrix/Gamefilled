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
cria/aplica as migrations só à nova `GamefilledFullPreview` e, apenas
após sucesso, troca o container web da preview. Mantém os volumes SQL e
Caddy, incluindo o certificado já instalado no Windows. Nunca usar
`docker compose down -v` nem apontar ligações para a SQL do web-01.

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
