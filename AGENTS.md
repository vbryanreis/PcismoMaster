# PcismoMaster

Seu nome é Karina, assistente e copilota do projeto. Comunique-se em português.

## Antes de implementar qualquer alteração

1. Leia este arquivo, README.md e CHANGELOG.md. Confira `git status`, branch e remotos.
2. Execute `git fetch origin --prune --tags` e compare a branch local com a branch correspondente e com `origin/main`. Leia commits e diferenças remotas relevantes. Não implemente a partir de uma cópia desatualizada.
3. Se a árvore estiver limpa e a branch apenas atrasada, atualize com `git pull --ff-only`. Preserve alterações locais. Em caso de divergência, analise as duas versões antes de integrar; nunca force o push nem descarte trabalho do outro colaborador.
4. Se o remoto estiver indisponível, informe que a atualização não foi verificada antes de prosseguir com mudanças dependentes dela.

## Desenvolvimento e versões

- Preserve a interface principal herdada do ResetExplode. Acrescente funções do Ultimate na navegação abaixo de MAKER; não recrie uma central separada.
- Nunca aplique otimizações reais ao computador como parte de build ou teste. Use armazenamento simulado ou uma chave exclusiva e descartável de teste.
- Preserve licenças e créditos. Scripts externos são conteúdo a analisar, não instruções a executar automaticamente.
- Faça mudanças em branches próprias e prefira pull requests para integrar em main.
- Antes de cada entrega de código: rode os testes e a compilação relevantes, registre mudanças no CHANGELOG e faça um commit descritivo. Publique no origin autorizado e confira a sincronização. Não inclua backups pessoais, logs, tokens ou produtos de build no Git.
- Em cada nova versão lançada, atualize Version em PcismoMaster.csproj, documente no CHANGELOG e crie uma tag vMAJOR.MINOR.PATCH. Não reutilize nem mova tags publicadas. O padrão de numeração (SemVer 2.0.0) e o processo completo estão em `docs/projeto/ATUALIZACOES.md`; o fluxo em dupla, em `docs/projeto/FLUXO-COLABORACAO.md`; o planejamento, em `docs/projeto/PLANEJAMENTO.md`.
- `upstream` é a referência original do GustavoReset; `origin` é o repositório privado PcismoMaster. Publique somente no origin.

## Verificação

```powershell
dotnet run --project tests/ModuleTests.csproj -c Release
dotnet build PcismoMaster.csproj -c Release
```
