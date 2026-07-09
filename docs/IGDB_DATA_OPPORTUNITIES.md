# IGDB Data Opportunities — Gamefilled V2

Este documento organiza dados que a IGDB disponibiliza e ideias para os transformar em funcionalidades próprias do Gamefilled.

## Princípio de produto

Não mostrar todos os campos numa única página. A informação deve ser organizada em secções progressivas e carregada apenas quando necessária, para manter a página clara, reduzir pedidos e respeitar os limites da API.

## Página de jogo

### Essencial

- Nome, slug, summary e storyline.
- Cover, artworks e screenshots.
- Data de lançamento principal.
- Géneros, temas, palavras-chave e perspetiva de jogador.
- Plataformas e modos de jogo.
- Developer, publisher, porting e supporting companies.
- Rating de utilizadores e rating agregado.
- Hypes e follows.
- Trailer e outros vídeos.

### Relações do jogo

- Franchise e collections.
- Jogos semelhantes.
- Parent game e versões.
- DLCs e expansions.
- Ports.
- Remakes e remasters.
- Standalone expansions.

**Ideia Gamefilled:** criar uma secção visual chamada **Game Universe**, mostrando a relação entre jogo principal, expansões, DLCs, versões e remasters.

### Lançamentos

- Release dates por plataforma.
- Região de lançamento.
- Estado do lançamento.
- Data humana e timestamp.

**Ideia Gamefilled:** uma timeline de lançamentos por plataforma e região.

### Informação técnica

- Game engines.
- Multiplayer modes.
- Player perspectives.
- Language supports.
- Websites e external games.

**Ideia Gamefilled:** uma área **Technical & Availability**, com motor, idiomas, multiplayer e links de lojas/plataformas.

### Classificação etária

- Age ratings.
- Rating category.
- Content descriptions.

**Ideia Gamefilled:** apresentar avisos de conteúdo de forma simples, evitando uma lista técnica difícil de interpretar.

## Página de empresa

- Nome, slug e descrição.
- Logo.
- País.
- Data de fundação.
- Estado da empresa.
- Dimensão.
- Empresa-mãe.
- Websites oficiais.
- Jogos desenvolvidos.
- Jogos publicados.
- Trabalhos de porting e apoio.

**Ideia Gamefilled:** uma página editorial com história da empresa, catálogo cronológico e separação entre developer, publisher e support studio.

## Descoberta e tendências

A IGDB disponibiliza popularity primitives atualizados regularmente, incluindo indicadores como visitas, jogadores interessados, jogadores ativos, desempenho Steam, reviews, vendas, wishlists e visualizações Twitch.

Possíveis listas:

- Most visited.
- Most wanted.
- Currently playing.
- Steam momentum.
- Twitch momentum.
- Rising games.
- Upcoming with most hype.

**Ideia Gamefilled:** substituir uma única página Trending por um **Trending Hub**, com várias métricas explicadas ao utilizador.

## Funcionalidades próprias do Gamefilled

- **Game Universe:** árvore de DLCs, expansões, remakes e versões.
- **Where to Play:** plataformas, lojas e links oficiais.
- **Release Timeline:** histórico por plataforma e região.
- **Company Network:** empresas-mãe, subsidiárias e colaborações.
- **Discovery Mixer:** combinação de género, plataforma, período, duração e rating.
- **Media Room:** trailers, artworks e screenshots num layout editorial.
- **Technical Profile:** engine, perspetiva, modos, multiplayer e idiomas.
- **Content Guide:** classificações etárias e descrições de conteúdo.

## Performance e arquitetura

- Usar cache para géneros, plataformas, empresas e tipos de popularidade.
- Fazer queries com campos explícitos, nunca `fields *` em produção.
- Agrupar IDs em pedidos batch.
- Evitar carregar todas as relações no primeiro pedido da página.
- Carregar secções secundárias sob pedido ou através de endpoints internos.
- Registar queries lentas e falhas da IGDB sem mostrar detalhes técnicos ao utilizador.
