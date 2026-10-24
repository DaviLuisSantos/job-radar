---
name: perfil-do-curriculo
description: Lê o currículo do candidato e preenche (ou atualiza) candidate/candidate.json, os fatos de onde saem os perfis do JobRadar. Use quando o autor mandar um currículo, pedir para "importar o currículo", "atualizar meus fatos/perfil a partir do CV" ou contar um fato novo (tirou um ticket, a carteira virou definitiva).
---

# Currículo → `candidate/candidate.json`

O `candidate.json` guarda **fatos** sobre o candidato: visto, formação, carteiras, tickets, experiência e linhas
de trabalho. Como buscar e pontuar (palavras de contexto, sinais achados rotulando) **não** entra aqui: isso é
ajustado contra o mercado e fica nos perfis (`Matching:Profiles` em `src/JobRadar.Worker/appsettings.Local.json`).
O formato e o efeito de cada campo no score estão em `candidate/candidate.schema.json`. Leia o esquema antes de
começar.

## Privacidade (não negociável)

- O currículo e o `candidate.json` ficam **só** em `candidate/`, que o git ignora. Confira com `git status` no fim
  que nada de lá aparece.
- Nenhum trecho do currículo vai para `docs/`, para o código, para fixture, para mensagem de commit ou para a
  memória. No Diário, registre só que os fatos foram atualizados, nunca quais são.
- Não envie o currículo a serviço externo nenhum.

## Passos

1. **Entrada.** O currículo é um arquivo em `candidate/` (PDF, DOCX ou texto) ou foi colado no chat. Se veio em
   outro lugar, peça para o autor mover para `candidate/`. Se já existe `candidate/candidate.json`, você vai
   **atualizar** esse arquivo, não substituir: fatos confirmados antes só mudam se o currículo ou o autor
   disserem outra coisa.
2. **Perfis existentes.** Se `src/JobRadar.Worker/appsettings.Local.json` existir, leia os nomes dos perfis. Cada
   `targets[].profile` reaproveita o nome do perfil que já cobre aquela linha de trabalho: os scores ficam
   gravados pelo nome, e trocar o nome joga fora o histórico de score. Linha nova ganha nome novo, em minúsculas.
3. **Extrair** só o que o currículo afirma:
   - `education`: título no idioma original, `field` em inglês. `recognisedInAustralia` é `false`, a menos que o
     autor diga que tem avaliação ou certificado australiano: curso técnico estrangeiro não é trade certificate.
   - `experience`: uma entrada por área. `years` somados pelo período no currículo. `role` como um anúncio
     australiano escreveria. `skills` são ferramentas e técnicas, como os anúncios escrevem ("C#", "forklift").
   - `licences` e `tickets`: só os que o currículo cita. Nomes australianos (HRWL, white card, HR).
4. **Perguntar o que o currículo não diz**, com `AskUserQuestion`, em poucas rodadas e só o que muda o score:
   - status e data da carteira de carro (provisória, definitiva a partir de quando) e carteiras de caminhão;
   - quais tickets tiraria com um curso curto (`obtainable`) e quais licenças estão fora de alcance, e por quê;
   - se alguma formação é reconhecida na Austrália;
   - linhas de trabalho desejadas (`targets`), locais, se aceita FIFO, se quer o segundo ano (88 dias);
   - o que não quer fazer (`avoid`).
   O que ficar sem resposta vai para `openQuestions`, não vira palpite.
5. **Gravar** `candidate/candidate.json` com `"$schema": "./candidate.schema.json"` e `schemaVersion` do esquema.
   Confira contra o esquema: campos obrigatórios, enums, meses no formato `AAAA-MM`, nenhum campo fora do
   esquema.
6. **Mostrar** ao autor um resumo do que entrou ou mudou (por campo, sem colar o currículo) e as
   `openQuestions` que sobraram.

## O que vem depois

Os tickets, as carteiras pesadas e a formação reconhecida viram o estado do candidato em cada requisito:
`import-candidate candidate/candidate.json --dry-run` mostra o que mudaria, e sem `--dry-run` grava. O relatório
lista o que o catálogo de requisitos ainda não tem (white card, forklift…). O resto dos fatos ainda não gera
perfil sozinho (ver `docs/plano.md`): use o `candidate.json` como referência para revisar o `appsettings.Local.json`
à mão. Qualquer mudança deve ser medida com `calibrate` (antes) e `calibrate --rescore` (depois) contra os rótulos.
