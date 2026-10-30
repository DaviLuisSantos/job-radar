---
name: regras-do-score
description: Regras do score do JobRadar (FitScorer, ScoringPolicy, MarketRules, elegibilidade, perfis, requisitos do candidato, rótulos e calibragem) e o teste de paridade com o protótipo. Use antes de mexer em src/JobRadar.Domain/Matching, em pesos, perfis, requisitos, elegibilidade remota, salário, ou ao rodar/ler calibrate.
---

# Regras do score

## Paridade (mexeu em `Domain/Matching`? rode)

```bash
JOBRADAR_PARITY_FILE=D:\Desenvolvimento\job-tracker\data\parity\scoring-parity.json JOBRADAR_LOCAL_SETTINGS=D:\Desenvolvimento\job-radar-au\src\JobRadar.Worker\appsettings.Local.json dotnet test --project tests/JobRadar.Domain.Tests
```

- Paridade do score (`[ParityFact]`, `JOBRADAR_PARITY_FILE`): 5.388 vagas reais exportadas do protótipo,
  fora do repo (são anúncios reais), pontuadas com o bloco de elegibilidade do arquivo local (`JOBRADAR_LOCAL_SETTINGS`;
  no checkout principal ele é achado sozinho, num worktree aponte para o do checkout principal). **Mexeu em
  `Domain/Matching`? Rode.** Mudança de regra intencional quebra a paridade: aí registre no `docs/plano.md` e incremente
  `FitScorer.RulesVersion`. Os casos com texto real de elegibilidade ficam em `scoring-cases.Local.json`, ao lado do
  arquivo local (`[LocalCasesFact]`, `JOBRADAR_LOCAL_CASES`); os testes públicos usam o mercado fictício de `TestMarkets`.

## Score no pipeline

**Score** — `ListingScoringService.ScoreAllAsync`: para cada perfil, pontua com `FitScorer` todas as
vagas que **as buscas daquele perfil** acharam e grava em `listing_scores` (uma linha por vaga × perfil,
com a explicação em jsonb e o `rules_hash`). Recalcula tudo todo dia, porque a idade muda o score, e no fim
apaga os scores que a passada não gravou (vaga que as buscas do perfil não acham mais, perfil removido).

O `FitScorer` lê a descrição **só** nas regras de requisito e de elegibilidade; sinais do perfil ficam em título e resumo.

## Perfis, fatos e requisitos

Perfis: `Matching:Profiles` em `src/JobRadar.Worker/appsettings.Local.json` (**fora do git**; modelo em
`appsettings.Local.example.json`). Cada palavra-chave é buscada em cada local; as buscas da coleta são a
união dos perfis (busca de dois perfis roda uma vez, nas fontes extras dos dois). Os **fatos** do candidato (formação, carteiras, tickets, experiência) ficam à parte, em
`candidate/candidate.json` (fora do git, como o currículo; esquema e exemplo fictício versionados), preenchido
pela skill `perfil-do-curriculo`. Fato vem do currículo; palavra-chave, contexto e sinal são ajustados contra o
mercado e ficam no perfil. Requisitos de anúncio (anos de experiência, trade, carteira, ticket) são itens do
`RequirementCatalog`, que o perfil cita por id em `Requirements:Items`: frase nova de requisito entra no item, não
no perfil, e vale para todo perfil que usa o item.

## Calibragem

Mudou peso ou regra? Rode `calibrate` (os scores gravados: o antes) e
`calibrate --rescore` (pontua de novo em memória com as regras do build, sem gravar: o depois). Para medir uma
branch contra os rótulos reais, rode-a num worktree no trabalhopc com o `appsettings.Local.json` copiado.

## Armadilhas

- Rótulo vale para o estado do candidato quando foi dado: uma vaga ruim porque pedia carteira HR vira boa quando
  ele tira a carteira (`requirement truck-hr have`). Mudou um estado para "tenho"? Reveja os rótulos das vagas que
  pedem aquele item antes de ler a calibragem como regressão. `--assume` mostra quais sobem.
- `Dismissed` (o candidato descartou) e `Declined` (o empregador recusou) não são a mesma coisa: na calibragem
  o primeiro conta como ruim e o segundo como **boa**. Não junte os dois de novo.
- Um perfil só pontua as vagas que as buscas **dele** acharam. Mudou a lista de palavras-chave? As vagas
  antigas continuam ligadas às buscas antigas pelo `search_key` (`"palavra @ local"` em minúsculas).
- Score: `FitScorer` é a **única** implementação, para todo país; a ordem das fases faz parte da regra (ver o comentário
  da classe). O que uma frase quer dizer é do mercado do **perfil** (`MarketRules`, escolhida pelo `JobMarket`: AU, BR, LatamRemote,
  GlobalRemote), não do país da vaga, que só diz onde ela fica; os padrões do AU
  ficam em `ListingPatterns`, menos quem pode se candidatar: isso é dado, o bloco `Matching:Markets:<mercado>:Eligibility`
  do arquivo local (`EligibilityRules`, impresso por `eligibility <mercado>`); os pesos, todos na `ScoringPolicy` (número solto
  no `FitScorer` é regressão: peso novo entra na política, que está no `rules_hash`). `".NET"` não casa dentro de `"ASP.NET"`
  (fronteira de token), e o "no" de "no experience" nega a frase de entrada seguinte — as duas coisas
  vêm do protótipo, e a paridade depende delas. Desde a `RulesVersion` 8 a carteira de caminhão diverge do
  protótipo de propósito (−40, não −20), e desde a 9 "Assistant … Manager" é liderança: o teste de paridade conta
  esses casos à parte. `ExcludedLocations` de um perfil exclui pelo local (o de fazenda exclui Perth);
  `ExcludedTitles`, por uma palavra do título (júnior, Java); `OnSiteLocations`, quando a vaga é presencial
  fora delas (`MarketRules.OnSite`). Regra que é do candidato vai para o perfil; o que uma frase quer dizer, para o mercado.
  O enum se chama `JobMarket` porque `Market` colide com o namespace `JobRadar.Domain.Market` (o do Raio-X).
- Remoto: a vaga fica em `Country.Remote` (gravado `ZZ`) e diz quem pode se candidatar em `Listing.Eligibility`
  ("Worldwide", "LATAM", "Brazil, Colombia"), que `RemoteEligibility` classifica parte a parte ("North America" não é
  "Americas"). Uma vaga cai num só mercado remoto: aberta ao mundo é do global, restrita a uma região com o Brasil é do
  LATAM. Salário lido do **anúncio** só conta com palavra de salário logo antes e sem palavra de benefício
  (`PayParser.Find`): vale-refeição, auxílio e rodada de investimento eram lidos como salário. O campo de salário da fonte
  é lido direto (`PayParser.Parse`). Regime e salário só pesam em perfil com `Compensation`.
