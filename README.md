# PcismoMaster

Versão 0.7.0. Consulte [CHANGELOG.md](CHANGELOG.md), [CONTRIBUTING.md](CONTRIBUTING.md) e [AGENTS.md](AGENTS.md). Antes de implementar, consulte as atualizações do origin e compare o histórico remoto.

Abra dist\PcismoMaster.exe. A interface principal é a base ResetExplode. Em FERRAMENTAS, a página Inicialização do Windows permite pausar o Windows Update por 365 dias, restaurar os seis valores anteriores e abrir as configurações do Windows Update. A pausa inclui correções de segurança e pode ser limitada pelo Windows. Nenhum ajuste é aplicado automaticamente.

A central separada foi retirada do fluxo. Licenças e créditos preservados em LICENSE e licenses/Ultimate-LICENSE.txt. O motor mantém backups em %LOCALAPPDATA%\PcismoMaster\ModuleBackups.

FERRAMENTAS contém a página Inicialização do Windows, com o ajuste Windows Update, e o Desinstalar Apps com varredura profunda de sobras (Lixeira e backup do registro e das tarefas em `%LocalAppData%PcismoMasterNINSTALLBACKUPS`). A pausa deriva do Ultimate (licença MIT em licenses).

A aba Overclock (DESEMPENHO) funciona como o MSI Afterburner em placas NVIDIA: sliders de Core Voltage, Power Limit, Temp. Limit, Core Clock, Memory Clock e Fan Speed, editor da curva V/F com undervolt, curva de fans, 5 perfis e monitoramento ao vivo. A camada NVAPI (curva V/F e offsets) foi portada do projeto [simple-nvidia-undervolt](https://github.com/vuplea/simple-nvidia-undervolt) (licença MIT em licenses/simple-nvidia-undervolt-LICENSE.txt).

A aba Profile Inspector (FERRAMENTAS · Drivers) lê e grava os perfis do driver NVIDIA (DRS da NVAPI), como o [NVIDIA Profile Inspector](https://github.com/Orbmu2k/nvidiaProfileInspector). Os catálogos de configurações (Npi/CustomSettingNames.xml e Npi/Reference.xml) vêm desse projeto, licença MIT em licenses/nvidiaProfileInspector-LICENSE.txt.

A sub-aba Painel NVIDIA mostra as configurações em 3D do Painel de Controle NVIDIA (globais e por programa) com os mesmos nomes. A instalação do driver (normal ou enxuta) aplica as configurações do Ultimate ajustadas à placa de cada PC, e a limpeza com o DDU (Wagnardsoft) roda no modo de segurança com registro em `%LocalAppData%\PcismoMaster\ddu-log.txt`.
