# Atualizações e lançamento de versões

Informações necessárias para atualizar o projeto e publicar uma nova versão.

## Versionamento (padrão profissional — SemVer 2.0.0)

> Esta é a regra que **todos os agentes e desenvolvedores** do projeto seguem igual — Bryan, Pedro, a Claudine (Claude), o Claude Code do Pedro e o Codex. Baseada em [Semantic Versioning 2.0.0](https://semver.org).

Formato: **`MAJOR.MINOR.PATCH`** (ex.: `1.4.2`), publicado como tag `vMAJOR.MINOR.PATCH` (ex.: `v1.4.2`).

**Qual número subir depende do TIPO da mudança, não de o número ter chegado a 9:**

- **PATCH** (`0.1.0` → `0.1.1`) — correção de bug ou ajuste pequeno, sem mudar o que o usuário espera e sem quebrar nada.
- **MINOR** (`0.1.5` → `0.2.0`) — função nova ou melhoria mantendo compatibilidade. Zera o PATCH.
- **MAJOR** (`0.9.0` → `1.0.0`) — mudança grande/incompatível (quebra comportamento ou fluxo existente). Zera MINOR e PATCH.

**Pontos que costumam gerar dúvida:**

- Cada número é um **inteiro independente** e **pode passar de 9**. Depois de `0.1.9`, outra correção vai para **`0.1.10`** (e não para `0.2.0`). Só se vira `0.2.0` quando a mudança é uma função nova (MINOR).
- Não existe "arredondar" `0.1.10` para `0.2.0`. A casa só sobe pelo tipo da mudança.
- Nunca use zero à esquerda (`0.1.01` é inválido; é `0.1.1`).
- Enquanto a MAJOR for **0** (`0.x.y`), o projeto está em desenvolvimento inicial: a API/interface ainda pode mudar bastante. A primeira versão "estável" pública é a `1.0.0`.
- (Opcional) Pré-lançamento e metadados existem no SemVer, ex.: `1.0.0-beta.1` ou `1.0.0+build.5`. Só usamos se precisar.

**Regra da dupla:** a cada versão nova que qualquer um de nós for lançar, **atualize o número da versão** conforme o tipo da mudança, antes de criar a tag. Não lance duas coisas na mesma versão nem reaproveite um número já publicado.

A fonte da verdade é `<Version>` em [`PcismoMaster.csproj`](../../PcismoMaster.csproj). A **tag** publicada tem que bater exatamente com esse número — o workflow de release falha de propósito se divergirem. O rótulo de versão exibido no app é lido automaticamente desse mesmo número.

## Compilar e testar localmente

```powershell
dotnet run --project tests/ModuleTests.csproj -c Release
dotnet build PcismoMaster.csproj -c Release
```

Para gerar o executável único como na release:

```powershell
dotnet publish PcismoMaster.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist --nologo
```

Lembre: **nunca** rode testes/builds que apliquem ajustes reais no Windows. Use armazenamento simulado ou chave de teste descartável.

## Checklist de entrega de código (todo PR)

1. Sincronizei a `main` (`fetch` + `pull --ff-only`) antes de começar — ver [FLUXO-COLABORACAO.md](FLUXO-COLABORACAO.md).
2. Trabalhei em branch própria.
3. Testes passam e o build compila em Release.
4. Registrei a mudança no `CHANGELOG.md`.
5. Commit descritivo e PR aberto com check verde; revisão pedida ao outro.
6. Não incluí backups, logs, tokens nem produtos de build.

## Processo de lançamento de uma versão

Depois que as mudanças da versão estão integradas na `main`:

1. Atualize `<Version>` em `PcismoMaster.csproj` para o novo número (ex.: `0.2.4`).
2. Adicione uma seção `## 0.2.4` no topo do histórico em `CHANGELOG.md`, listando o que mudou. **Essa seção vira as notas da release** — o workflow extrai exatamente o texto dela.
3. Commit e integração na `main` (via PR).
4. Com a `main` atualizada, crie e publique a tag:

```powershell
git switch main
git pull --ff-only
git tag v0.2.4
git push origin v0.2.4
```

5. O workflow **Lançar versão** compila, roda os testes, gera o `.exe` único e o `.zip`, e publica a Release no GitHub com as notas do CHANGELOG.

Regras: **nunca reutilize nem mova uma tag já publicada.** Se errar, crie a próxima PATCH. Uma tag só é publicada a partir da `main` com a versão do csproj batendo.

## O que o GitHub Actions faz

- **Validar PcismoMaster** (`push`, `pull_request`, manual): roda os testes e publica um pacote portátil nos *artifacts*. É o check que precisa ficar verde antes de integrar um PR.
- **Lançar versão** (push de tag `v*.*.*`): compila, testa, empacota e cria a Release.
