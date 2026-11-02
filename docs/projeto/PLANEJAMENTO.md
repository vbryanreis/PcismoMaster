# Planejamento do PcismoMaster

> Atualizado em 2026-11-02. Mantenha este documento vivo: mova itens entre as seções conforme o trabalho avança e anote quem está tocando cada coisa.

## Visão do produto

PcismoMaster é um aplicativo **Windows** (WPF / .NET 8) para diagnóstico, monitoramento e otimização de PC, derivado do **ResetExplode** e incorporando funções do **Ultimate**. A interface principal é a herdada do ResetExplode; as funções do Ultimate entram na navegação **abaixo de MAKER**, no grupo **OTIMIZAÇÃO** — sem recriar uma central separada.

Princípios inegociáveis:

- **Nenhuma otimização é aplicada automaticamente** ao computador em build ou teste. Testes usam armazenamento simulado ou chave descartável.
- Preservar **licenças e créditos** (LICENSE, `licenses/Ultimate-LICENSE.txt`).
- Publicar somente no `origin` (repositório privado). `upstream` é só referência do ResetExplode original.

## Pronto (resumo — detalhes no CHANGELOG)

- Interface de hardware/diagnóstico da base ResetExplode, abertura rápida com carregamento em paralelo.
- Abas/grupos: JOGOS, DESEMPENHO, FERRAMENTAS, MAKER e OTIMIZAÇÃO.
- OTIMIZAÇÃO → sub-aba **Inicialização do Windows** → **Windows Update** (pausar por 365 dias, restaurar valores anteriores com aviso quando não há backup, abrir configurações).
- RESET MAKER com drivers seriais oficiais embutidos; RESET DRIVER (NVIDIA/AMD); overlay personalizável sobre jogos; benchmarks (inclusive ray tracing DXR 1.1).
- Executável único autocontido + release automática por tag no GitHub Actions.
- Motor de backup/restauração do registro com testes de falha.

## Em andamento

<!-- Formato: - [responsável] descrição — branch -->

_(nada registrado no momento — adicione aqui ao pegar uma tarefa)_

## Backlog / ideias

<!-- Mova para "Em andamento" quando alguém começar. -->

- A definir em conjunto por Bryan e Pedro.

## Pendências de validação conhecidas

- Efeito da pausa do Windows Update após reinicialização.
- Recursos herdados do ResetExplode que ainda precisam de validação específica no contexto PcismoMaster.

## Como registrar uma nova ideia

1. Adicione uma linha em **Backlog / ideias** com uma frase clara do valor para o usuário.
2. Ao começar, mova para **Em andamento** com seu nome e a branch.
3. Ao concluir, descreva no `CHANGELOG.md` e remova daqui (ou mova para "Pronto" se for marco grande).
