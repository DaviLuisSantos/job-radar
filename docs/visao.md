# Visão — JobRadar

> Documento vivo. Alinhado em 2026-10-21; revisto em 2026-10-30, quando o repositório público deixou de carregar o
> vocabulário do primeiro mercado (fase 0b da [evolução](evolucao.md)). Mudou o escopo? Atualize aqui primeiro.

## Propósito

Acompanhar mercados de trabalho com números confiáveis e ajudar cada perfil a achar as vagas que de fato pode pegar:
um radar com fontes plugáveis, um score explicável por mercado e um retrato público, só agregado, do mercado
australiano.

## Para quem

| Público | Quando | O que usa |
|---|---|---|
| O autor | v1 | vagas que valem candidatura hoje, explicação do score, acompanhamento de candidaturas |
| Leitores do **Raio-X** (newsletter em português sobre o mercado australiano) | v2 | números agregados mensais |
| Usuários da ferramenta | v3, se houver audiência | perfil próprio, alertas próprios |
| Recrutadores | sempre | código, README, sistema no ar |

## Perguntas que o sistema responde

**Raio-X (público, sempre agregado)**
- Quantas vagas por cidade e área, e a tendência mês a mês
- Quantas vagas citam os termos que o retrato acompanha (`Market:Terms`, na configuração local)
- Faixa de salário/hora por área, quando o anúncio informa
- Quanto é casual, temporário ou contrato

**Matching (privado)**
- Quais vagas novas valem candidatura hoje, em cada mercado (Austrália, Brasil, remoto LATAM e global), e por quê
- O score concorda com os rótulos do usuário (calibragem)
- Em que pé está cada candidatura (status, datas, notas)

## Não-objetivos

- Não é site de vagas: não republica anúncios, aponta para a fonte
- Não se candidata sozinho
- Não contorna anti-bot nem abre páginas de vaga protegidas
- Não diz quem pode trabalhar onde: as regras de elegibilidade de cada mercado são dado do autor
  (`Matching:Markets:<mercado>:Eligibility`, no arquivo local), fora do repositório
- Não é tempo real: uma coleta por dia
- Não envia e-mail: gera o conteúdo; a distribuição é por plataforma pronta
- Não substitui dado oficial, complementa
- Raio-X público só da Austrália. Brasil e remoto entram só no Matching, de uso próprio (decidido em 2026-10-29:
  [evolucao.md](evolucao.md))

## Fontes de vagas

Domínio comum; cada site é uma integração que traduz o que ele tem para o modelo neutro. Hoje: Seek (Austrália),
Gupy (Brasil), Himalayas e Jobicy (remoto). Adzuna e Workforce Australia avaliados para a Austrália; Indeed e
LinkedIn fora. Detalhes em [fontes.md](fontes.md).

## Fases

| Fase | Entrega | Critério para avançar |
|---|---|---|
| **v1 — uso próprio** | coleta, score explicável, triagem, **acompanhamento de candidaturas**, retrato diário e relatório mensal, histórico importado do protótipo Python | paridade de score com o Python; uso diário; Python desligado |
| **v2 — Raio-X público** | página pública + JSON versionado, edição mensal, metodologia aberta, cruzamento com dado oficial, novas fontes, recorte por região (postcodes e setores oficiais) | 3 edições seguidas no prazo e reproduzíveis |
| **v3 — ferramenta aberta** | perfis de outros usuários, alertas personalizados, login | audiência da newsletter que justifique (número a definir) |

## Em aberto

- Áreas acompanhadas no Raio-X além de tecnologia (hospitality, construção, trabalho rural, limpeza, cuidados?)
- Cadência: newsletter mensal; post semanal?
- IA no score: só se melhorar a calibragem. Piloto de 2026-10-24 (Claude lendo requisitos): não melhorou.
- Número de leitores que libera a v3
