# Fluxo de colaboração em dupla

O PcismoMaster é mantido por **Bryan** (`vbryanreis`) e **Pedro**, cada um no seu PC, com a mesma branch `main` no GitHub (`origin`). A regra de ouro é: **nunca comece a mexer a partir de uma cópia desatualizada** e **nunca force o push nem descarte o trabalho do outro**.

## 1. Sempre sincronize ANTES de mexer

No começo de qualquer sessão de trabalho, da pasta do projeto:

```powershell
git status
git fetch origin --prune --tags
git switch main
git pull --ff-only
```

- `git fetch` traz tudo do GitHub sem alterar seus arquivos. É aqui que vemos se o outro já publicou algo.
- `git pull --ff-only` só avança a `main` local se não houver divergência. Se ele falhar, **pare e analise** antes de qualquer coisa (veja a seção de conflitos).
- Se a árvore estiver suja (mudanças não commitadas), preserve-as primeiro (commit numa branch ou `git stash`).

Se o GitHub estiver indisponível e não der para verificar, registre isso e evite mudanças que dependam da versão remota.

## 2. Trabalhe em uma branch própria, nunca direto na `main`

```powershell
git switch -c feat/nome-da-funcao   # ou fix/, docs/, chore/
```

Prefixos: `feat/` (função nova), `fix/` (correção), `docs/` (documentação), `chore/` (manutenção).

Um assunto por branch. Isso reduz a chance de os dois mexerem nos mesmos arquivos ao mesmo tempo.

## 3. Combine quem mexe em quê

Antes de pegar uma tarefa grande, avise o outro (e registre no [PLANEJAMENTO.md](PLANEJAMENTO.md), coluna "Em andamento", com o nome de quem está fazendo). Evitar que os dois editem o mesmo arquivo é muito mais barato do que resolver conflito depois.

## 4. Entregue via pull request

```powershell
git add <arquivos-alterados>
git commit -m "feat: descreva a mudança"
git push -u origin feat/nome-da-funcao
```

Abra um pull request para `main` no GitHub e peça revisão ao outro. O workflow **Validar PcismoMaster** roda os testes e compila a cada push/PR; só integre com o check verde.

Depois de integrar o PR, atualize sua `main` local:

```powershell
git switch main
git pull --ff-only
git branch -d feat/nome-da-funcao
```

## 5. Resolvendo divergência / conflito

Se `git pull --ff-only` recusar, é porque a `main` remota andou e a sua também. **Não force nada.** Traga as mudanças remotas por cima das suas com rebase da sua branch de trabalho:

```powershell
git fetch origin
git switch feat/minha-branch
git rebase origin/main
# resolva os conflitos arquivo por arquivo, depois:
git add <arquivos-resolvidos>
git rebase --continue
```

Ao resolver, **leia as duas versões** e preserve o que o outro fez. Em dúvida, pergunte antes de sobrescrever. Nunca use `git push --force` na `main`; em branch própria, só `--force-with-lease` e avisando.

## 6. O que NÃO vai para o Git

Definido no `.gitignore`: `bin/`, `obj/`, `dist/`, `*.exe`, `*.dll`, `*.pdb`, arquivos de IDE, logs. Não comite backups pessoais, tokens nem produtos de build.

## Checklist rápido

- [ ] `git fetch` + `pull --ff-only` na `main` antes de começar.
- [ ] Branch própria com prefixo e assunto único.
- [ ] Avisei o Pedro do que vou mexer (tarefa grande).
- [ ] Testes e build passando localmente.
- [ ] CHANGELOG atualizado (se for mudança de código relevante).
- [ ] PR aberto, check verde, revisão pedida.
