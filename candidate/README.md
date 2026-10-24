# Candidate facts

Who the candidate is, as data: visa, training, licences, tickets, experience and the lines of work wanted.
Matching profiles (`Matching:Profiles`) are drafted from these facts; what is tuned against the market
(keywords that pay off, context words, signals found by labelling) stays in the profiles.

- `candidate.schema.json`: the format, with what each fact does to the score.
- `candidate.example.json`: a fictional candidate.
- `candidate.json` and the CV itself: yours, in this folder, **ignored by git**.

To fill it from a CV, drop the CV here and run the `/perfil-do-curriculo` skill in Claude Code: it reads the
CV, fills the schema and asks what the CV does not say.
