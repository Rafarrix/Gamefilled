# Gamefilled V2 — preview privada permanente no homelab

Objetivo: ter uma V2 modernizada **visitável em HTTPS na LAN** e atualizá-la
quando um conjunto de mudanças estiver pronto. Não substitui a aplicação
atual nem utiliza a base de dados de produção.

## Ambiente isolado

- Compose project: **gamefilled-v2-preview**, nunca o projeto atual.
- SQL Server Express e volume separados: **GamefilledV2Preview** (não Gamefilleddb).
- ASP.NET Core Identity + Caddy, certificados TLS internos.
- URL de browser: **https://preview.gamefilled.pt:8444** (apenas LAN).
- O início da página mostra o commit da imagem em execução.
- O primeiro ambiente é **técnico**: autenticação, pesquisa, detalhe de jogos,
  biblioteca, reviews e perfil privado. A homepage e a comunidade da V2 ainda
  não têm paridade com o site existente.

Nenhum merge no GitHub inicia containers ou mexe no Proxmox. O deploy
é manual e só a partir de um checkout independente.

## Gate antes de iniciar — executar na VM Debian

Nunca assumir o IP 192.168.1.21 ou 192.168.1.85 sem confirmar. Consultar:

~~~bash
ip -4 -br addr
free -h
df -h
docker compose ls
ss -lnt | grep 8444 || true
~~~

Verificar capacidade disponível: pelo menos 5 GiB de RAM disponível na VM
e 10 GiB de espaço livre para a preview. O SQL tem limite de 3 GiB, mas o
projeto total consome mais. Confirmar que 172.28.81.0/24 não colide com
outras redes Docker ou VPN e que a porta 8444 está livre.

**Antes de mexer no homelab:** backup externo e restauro testado dos dados
importantes da aplicação atual; verificar que a aplicação existente continua
saudável. Permitir TCP 8444 na firewall da VM/Proxmox **só à LAN real**.
Rever o comportamento de Docker port publishing e testar que não é acessível
da WAN. Sem port forwarding no router, UPnP, túnel ou DNS público.

## Checkout exclusivo e segredos

Na VM, como utilizador com permissões Docker:

~~~bash
sudo mkdir -p /opt/gamefilled-preview
sudo chown "$USER":"$USER" /opt/gamefilled-preview
git clone --branch master https://github.com/Rafarrix/Gamefilled.git /opt/gamefilled-preview/repo
cd /opt/gamefilled-preview/repo
cp preview/.env.preview.example preview/.env.preview
mkdir -p preview/local/keys preview/local/secrets
chmod 600 preview/.env.preview
~~~

Editar preview/.env.preview **localmente** e fornecer PREVIEW_LAN_IP
correspondente à VM, uma password SQL de preview única e aleatória (20+
caracteres) e as credenciais IGDB. O exemplo deixa o IP vazio de propósito.

Colocar a chave Resend real e exclusiva para testes, autorizada a enviar
para gamefilled.pt, em preview/local/secrets/resend_api_key. Não mostrar
credenciais em chats, logs, screenshots ou comandos no histórico. São
necessários emails reais para validar a confirmação da conta de teste.

~~~bash
sudo chown -R 1654:1654 preview/local/keys
sudo chmod 700 preview/local/keys
sudo chgrp 1654 preview/local/secrets
sudo chmod 750 preview/local/secrets
sudo chgrp 1654 preview/local/secrets/resend_api_key
sudo chmod 640 preview/local/secrets/resend_api_key
~~~

## Primeiro arranque (manual, com confirmação)

~~~bash
cd /opt/gamefilled-preview/repo
bash preview/previewctl.sh check
bash preview/previewctl.sh deploy
~~~

O comando deploy pede para escrever **START PREVIEW**. Constrói a revisão
aprovada da master; inicia apenas a SQL da preview, executa as migrations
**em GamefilledV2Preview**, inicia o web/Caddy e verifica os principais
endereços HTTPS com o certificado local. Não mexe na stack atual.

## Abrir no Windows

No Windows, iniciar o Bloco de Notas como administrador e editar
C:\Windows\System32\drivers\etc\hosts, acrescentando uma linha:

~~~text
IP_REAL_DA_VM preview.gamefilled.pt
~~~

Substituir IP_REAL_DA_VM pelo IP real validado no Debian. Não apontar
DNS público para a preview. Depois da primeira inicialização, exportar
o **certificado público** da CA Caddy, nunca a chave privada:

~~~bash
docker compose --project-name gamefilled-v2-preview \
  --env-file preview/.env.preview -f compose.preview.yaml \
  exec -T caddy cat /data/caddy/pki/authorities/local/root.crt > preview-root.crt
openssl x509 -in preview-root.crt -noout -fingerprint -sha256
~~~

Copiar preview-root.crt para o computador pessoal por um canal de
confiança, validar que o fingerprint SHA-256 coincide e importar no
armazenamento **Current User → Trusted Root Certification Authorities**.
Confiar numa CA permite-lhe emitir certificados aceites pelo browser:
instalar apenas a CA verificada da tua própria preview.

Abrir **https://preview.gamefilled.pt:8444**. As contas e os dados
de teste são separados dos existentes.

## Testar e atualizar ocasionalmente

~~~bash
cd /opt/gamefilled-preview/repo
bash preview/previewctl.sh status
bash preview/previewctl.sh smoke
~~~

status mostra quais os serviços ativos e a revisão instalada, sem revelar
segredos. smoke verifica TLS, a homepage, health live/ready, pesquisa e a
necessidade de autenticação no perfil. Fazer também os testes funcionais
em preview/ACCEPTANCE.md e comparar visualmente com a V2 atual.

Quando uma funcionalidade tiver CI aprovada e merge na master, validar
o plano para migrations e criar um **backup da base da própria preview**
se for necessário preservar contas e reviews de teste. A atualização é:

~~~bash
cd /opt/gamefilled-preview/repo
git pull --ff-only origin master
bash preview/previewctl.sh check
bash preview/previewctl.sh deploy
bash preview/previewctl.sh status
~~~

Se SQL da preview estiver ativo, pede **UPDATE PREVIEW**. Apenas o
projeto de preview é reconstruído e reiniciado. O commit visível no site
distingue a versão anterior da nova. Um merge no GitHub **não atualiza
automaticamente** o que está no browser.

## Parar, rollback e limites

~~~bash
bash preview/previewctl.sh stop
~~~

Para voltar a iniciar, usar deploy após as verificações. Não usar
docker compose down -v. Se uma atualização falhar, recuperar a imagem
anterior de um commit testado e, se necessário, restaurar apenas o backup
da base da preview (não existe rollback automático de migrations). Não
eliminar volumes da produção.

Limitações: a preview usa SQL sa apenas numa rede privada interna,
Data Protection keys locais, e não está preparada para acesso público.
Faltam paridade da homepage, comunidade, moderação, políticas de
privacidade, testes de carga, cópias de segurança externas completas
e credenciais de base de dados com privilégios reduzidos. A relevância
de pesquisas/rankings IGDB continua sujeita a calibração humana.
