---
name: site-web
description: Site de triagem do JobRadar (Blazor + MudBlazor em src/JobRadar.Web): escopo de serviço por operação, prerender, tema e paleta, textos em pt-BR e a lista de triagem. Use antes de mexer em páginas, componentes, CSS, textos do site ou na lista de triagem.
---

# Site (JobRadar.Web)

- Interface em português: texto que o usuário lê mora em `Text/Wording.cs` ou no markup, com datas e
  números em pt-BR (`Wording.Culture`); `InvariantCulture` só para o que não é exibido.
- Web: componente interativo chama serviço **só** por `Backend.RunAsync` (um escopo, e um `DbContext`, por
  operação). Injetar `TriageService` direto no componente prende um `DbContext` à aba inteira.
- Web com prerender: `OnInitialized`/`OnParametersSet` rodam duas vezes (estático e interativo). Gravação que muda o que
  a própria página lê (a visita do "Hoje") vai em `OnAfterRenderAsync`, que só roda no interativo.
- Web, cores: a paleta é uma só (`JobRadarTheme`). O `app.css` tira as cores das variáveis `--mud-palette-*`, e a paleta
  escura vai no `<head>` (`JobRadarTheme.DarkCss`, sob `html[data-theme=dark]`, que um script no head põe antes da primeira
  pintura): o HTML pré-renderizado já sai no tema certo. Cor nova entra na paleta, não como hex no CSS.
- Lista de triagem: agrupar duplicatas (`Duplicates.Collapse`) **antes** de paginar, e aplicar cada ação a
  todas as cópias (`TriageRow.AllIds`).
- Para olhar o site com dados, rode-o contra uma cópia (`copy-database` para o SQL Server local), nunca contra o banco
  de produção (ver a skill `operar-worker`).
