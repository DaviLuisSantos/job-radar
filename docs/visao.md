# Visão — JobRadar AU

> Documento vivo. Alinhado em 2026-10-21, antes da primeira linha de código.
> Mudou o escopo? Atualize aqui primeiro.

## Propósito

Mostrar, com números confiáveis, como está o mercado de trabalho australiano para quem
chega com Work and Holiday (WHV), e ajudar cada pessoa a achar as vagas que de fato
pode pegar.

## Para quem

| Público | Quando | O que usa |
|---|---|---|
| O autor | v1 | vagas que valem candidatura hoje, explicação do score, acompanhamento de candidaturas |
| Leitores do **Raio-X WHV** (newsletter em português) | v2 | números agregados mensais |
| Usuários da ferramenta | v3, se houver audiência | perfil próprio, alertas próprios |
| Recrutadores | sempre | código, README, sistema no ar |

## Perguntas que o sistema responde

**Raio-X (público, sempre agregado)**
- Quantas vagas por cidade e área, e a tendência mês a mês
- Fração que aceita WHV, exige cidadania/PR ou pede "full working rights"
- Faixa de salário/hora por área, quando o anúncio informa
- Quanto é casual, temporário ou contrato (compatível com o limite de 6 meses por empregador)

**Matching (privado)**
- Quais vagas novas valem candidatura hoje, e por quê
- O score concorda com os rótulos do usuário (calibragem)
- Em que pé está cada candidatura (status, datas, notas)

## Não-objetivos

- Não é site de vagas: não republica anúncios, aponta para a fonte
- Não se candidata sozinho
- Não contorna anti-bot nem abre páginas de vaga protegidas
- Não dá orientação migratória (não diz se alguém é elegível ao visto)
- Não é tempo real: uma coleta por dia
- Não envia e-mail: gera o conteúdo; distribuição por plataforma pronta ou canal da Sirene WHV
- Não substitui dado oficial, complementa
- Só Austrália

## Fontes de vagas

Domínio comum; cada site é uma integração que traduz o que ele tem para o modelo
neutro. v1 só Seek; v2 Adzuna e Workforce Australia; Indeed e LinkedIn fora.
Detalhes em [fontes.md](fontes.md).

## Fases

| Fase | Entrega | Critério para avançar |
|---|---|---|
| **v1 — uso próprio** | coleta do Seek, score explicável, triagem, **acompanhamento de candidaturas**, retrato diário e relatório mensal, histórico importado do protótipo Python | paridade de score com o Python; uso diário; Python desligado |
| **v2 — Raio-X público** | página pública + JSON versionado, edição mensal, metodologia aberta, cruzamento com dado oficial, novas fontes, **vagas que contam para os 88 dias regionais** (postcodes e setores oficiais) | 3 edições seguidas no prazo e reproduzíveis |
| **v3 — ferramenta aberta** | perfis de outros usuários, alertas personalizados, login | audiência da newsletter que justifique (número a definir) |

## Em aberto

- Áreas acompanhadas no Raio-X além de tecnologia (hospitality, construção, farm/regional, limpeza, cuidados?)
- Cadência: newsletter mensal; post semanal no canal da Sirene?
- IA no score: só se melhorar a calibragem. Piloto de 2026-10-24 (Claude lendo requisitos): não melhorou; ver Diário do plano.
- Número de leitores que libera a v3
