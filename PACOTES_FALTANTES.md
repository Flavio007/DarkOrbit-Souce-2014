# Pacotes faltantes e incompatibilidades

Inventario comparando o servidor com o cliente decompilado:

- Cliente atual: `G:\xpp\Tools\swf_work\main_current`
- Cliente legacy: `G:\xpp\Tools\swf_work\main_old_2011`
- O caminho `G:\xpp\Tools\swf\_work` citado originalmente nao existe; o dump encontrado foi `swf_work`.

Nenhum build foi executado. Os contratos abaixo foram obtidos por leitura dos serializers, dispatchers e handlers do cliente, e por busca dos IDs no servidor.

## Convencoes de protocolo

### Netty atual

O cliente le e escreve um `short ID` no inicio do comando. Depois que o dispatcher consome esse ID, os payloads abaixo sao descritos na ordem dos campos restantes. Inteiros podem usar rotacoes (`intR4`, `intR6`, `intR9` etc.) e os shorts constantes sao ruido obfuscado do protocolo; eles precisam permanecer no wire.

No servidor, o frame e tratado por `Net\netty\Handler.cs` e os helpers de bytes estao em `Utils\Bytes.cs`.

### Legacy

Comandos de entrada legacy chegam como texto terminado por `\n` e sao separados por `|`. As respostas usam normalmente:

```text
0|<modulo>|<comando>|<parametros...>
```

Quando transportados pelo `LegacyModule` (`ID 4224`), o campo UTF contem a mensagem sem o prefixo `0|`. Portanto, nas tabelas legacy abaixo, a forma `0|...` e a forma final observada no cliente.

### Status

- **IMPLEMENTADO**: existe serializer/handler funcional com o mesmo contrato.
- **PARCIAL**: existe parte do fluxo, mas nao a semantica completa.
- **FALTANDO**: nao foi encontrado no servidor.
- **INCOMPATIVEL**: existe o mesmo ID ou um pacote parecido, mas com outro payload/uso.
- **MAPEAR**: o contrato foi localizado, mas nomes semanticos ainda sao inferidos pelos usos.

## 1. Resumo prioritario

| Area | Resultado |
|---|---|
| Sector Control / beacons capturaveis | **FALTANDO**: o cliente possui estado, progresso, contestacao, tickets e lobby; o servidor possui somente enums e assets genericos. |
| Batalha por influencia | **FALTANDO**: `ID 4246` para os tres placares de faccao e `ID 7114` para o placar individual. |
| Capture The Beacon (CTB) legacy | **FALTANDO**: o servidor tem apenas constantes em `ServerCommands.cs`; nao ha estado nem emissao das mensagens `ctb`. |
| TDM | **FALTANDO**: os contratos de placar/resultado do cliente nao foram encontrados no servidor. |
| Battle Station | **IMPLEMENTADO**, mas e captura de estacao/clan e nao substitui Sector Control. |
| Beacons do LoW | **IMPLEMENTADO**: relay stations usam carga `0..100` e `AssetCreateCommand`. |
| Ores e quests modernos | **IMPLEMENTADO/PARCIAL** no estado atual do workspace; os arquivos correspondentes ja existem. |

## 2. Sector Control e beacons capturaveis

### 2.1 O modelo visual que deve ser reutilizado

O cliente registra o asset `mapasset_sector_control_beacon` e o material `SectorControlBeaconMaterial`. O modelo `_SafeCls_2101` possui:

| Campo obfuscado | Uso observado |
|---|---|
| `_SafeStr_8112` | capacidade maxima; inicializada em `100` |
| `_SafeStr_18046` | progresso/pontos atuais |
| `_SafeStr_8615` | faccao proprietaria ou associada |
| `_SafeStr_15794` | vetor de faccoes presentes/contestantes |
| `_SafeStr_3384` | valor temporal de lock/cooldown visual |
| `_SafeStr_7241`, `_SafeStr_15140`, `enabled` | estados de interacao/apresentacao |

O progresso normalizado e `currentProgress / 100`. A carga da barra e outro objeto do cliente, `_SafeCls_2103`:

```text
sectorHash:String
faction:int
direction:int
progress:Number       // percentagem, 0..100
```

As direcoes usadas pela UI sao `0=neutro`, `1=conquistando` e `2=neutralizando`. A mesma carga pode ser reutilizada em qualquer tela que precise exibir a carga do beacon: basta manter o `sectorHash`, faccao, direcao e percentagem.

Fontes principais no dump:

```text
G:\xpp\Tools\swf_work\main_current\decompiled_scripts\scripts\_SafePkg_2102\_SafeCls_2101.as
G:\xpp\Tools\swf_work\main_current\decompiled_scripts\scripts\_SafePkg_2104\_SafeCls_2103.as
G:\xpp\Tools\swf_work\main_current\decompiled_scripts\_SafePkg_2340\SectorControlProgressBarMediator.as
G:\xpp\Tools\swf_work\main_current\decompiled_scripts\scripts\_SafePkg_1417\Settings3D.as
```

### 2.2 Estado principal do beacon — servidor -> cliente

`_SafeCls_1055`, **ID 15267**, e o pacote principal de atualizacao do beacon. O handler `_SafeCls_1503` procura o asset pelo hash, atualiza progresso/faccao/faccoes contestantes/timer e publica `UpdateProgressBarModelProgress`.

Payload observado no `write`, depois do ID:

```text
captureProgress:intR6
short 12530
lockTimer:double
short -12248
factionCount:int
repeat factionCount:
    faction:_SafeCls_583
currentFaction:_SafeCls_583
sectorHash:UTF
```

`_SafeCls_583` e o enum de faccao: `0=NONE`, `1=MMO`, `2=EIC`, `3=VRU`. O significado exato de `lockTimer` foi inferido pelo uso visual: o modelo o transforma em lock/cooldown e escolhe textura por faccao.

| ID | Nome logico | Status no servidor |
|---:|---|---|
| 15267 | `SectorControlBeaconUpdate` | **FALTANDO** |

O servidor possui `AssetCreateCommand` (`ID 13718`) e os tipos numericos de asset, mas nao possui esse serializer especifico nem um manager que mantenha `captureProgress`, owner, contestantes e lock por beacon.

### 2.3 Mostrar/ocultar a carga visual

`_SafeCls_799`, **ID 24512**, e um pacote separado para selecionar/ativar a barra do setor. O handler `_SafeCls_1522` publica `UpdateProgressBarModelSector`; quando desativado, limpa a barra.

Payload observado:

```text
short 22786
sectorHash:UTF
visible:Boolean
short -8553
```

| ID | Nome logico | Status no servidor |
|---:|---|---|
| 24512 | `SectorControlBeaconProgressVisibility` | **FALTANDO** |

Esse pacote e particularmente util como carga reutilizavel: o servidor pode envia-lo ao entrar na area, ao iniciar uma disputa e ao trocar o beacon selecionado, seguido do `ID 15267` com o progresso atual.

### 2.4 Tickets, jogadores e bonus dos pontos

`_SafeCls_996`, **ID 19068**, entrega em lote tres vetores para o `SectorControlProxy`:

```text
Vector<FactionTicketEntry>       // _SafeCls_1090, ID 16633
Vector<FactionPlayerEntry>       // _SafeCls_623,  ID 18603
Vector<ControlPointBonusEntry>   // _SafeCls_1076, ID 1885
```

Tipos aninhados encontrados:

| ID | Campos observados | Uso no cliente |
|---:|---|---|
| 16633 | `faction`, `int`, `int` | tickets/placar por faccao |
| 18603 | `faction`, `int`, `int` | quantidade de jogadores por faccao |
| 1885 | `faction`, `Number`, `_SafeCls_573` | bonus de ponto de controle |
| 19265 | `uint` + short | modulo aninhado de bonus; nao confundir com um comando top-level |

| ID | Nome logico | Status no servidor |
|---:|---|---|
| 19068 | `SectorControlFactionMetricsUpdate` | **FALTANDO** |
| 16633 / 18603 / 1885 | tipos aninhados do pacote acima | **FALTANDO** |

O proxy tambem tem alerta de setor instavel: quando a proporcao de tickets fica abaixo de aproximadamente `5%`, publica `PingUnstableSectors`. Isso confirma que o vetor de tickets nao e apenas informativo; ele participa do comportamento do evento.

### 2.5 Lobby e detalhe da partida

Pacotes recebidos pelo `SectorControlProxy`:

| ID | Nome logico | Payload observado | Status |
|---:|---|---|---|
| 12560 | `SectorControlMatchOverview` | `minLevel:int`, tres inteiros de configuracao e `Vector<MatchItem>` | **FALTANDO** |
| 23446 | `MatchItem` aninhado | sete campos: `int`, `int`, `uint`, `int`, `totalTickets:int`, `int`, `int` | **FALTANDO** |
| 12308 | `SectorControlMatchDetail` | `Vector<FactionTicketEntry>`, `int`, `Vector<FactionPlayerEntry>`, `int` | **INCOMPATIVEL**: o servidor ja usa o ID 12308 para outro payload de UBA |

Requests do lobby cliente -> servidor:

| ID | Nome logico | Payload | Status |
|---:|---|---|---|
| 15848 | `RequestSectorControlOverview` | nenhum campo funcional | **FALTANDO** |
| 14405 | `SelectSectorControlMatch` | `matchId:int` | **FALTANDO** |
| 25842 | `JoinLeaveSectorControlMatch` | `matchId:int` | **FALTANDO** |
| 19501 | `SectorControlExit` | nenhum campo funcional | **FALTANDO** |
| 26416 | `SectorControlConfirm` | `confirmed:Boolean` | **FALTANDO** |
| 20135 | `SectorControlLeaveGame` | nenhum campo funcional | **FALTANDO** |

Nao foi encontrado no cliente um request chamado `capture`, `claim` ou equivalente. A evidencia indica que a captura e uma consequencia do estado da partida: o servidor deve alterar o estado do beacon e emitir `15267`, em vez de esperar um comando de captura dedicado.

## 3. Batalha por influencia

### 3.1 Placares globais das faccoes

`_SafeCls_635`, **ID 4246**, atualiza o placar global da janela de influencia. O wire order e importante: os campos obfuscados sao gravados como:

```text
scoreVRU:double    // _SafeStr_4602
scoreEIC:double    // _SafeStr_14250
scoreMMO:double    // _SafeStr_10668
```

O handler repassa os mesmos valores ao proxy na ordem semantica MMO/EIC/VRU. Trocar essa ordem no serializer faz a UI exibir as faccoes erradas.

`_SafeCls_725`, **ID 7114**, atualiza o placar individual:

```text
playerInfluenceScore:double
```

| ID | Nome logico | Status no servidor |
|---:|---|---|
| 4246 | `InfluenceFactionScoresUpdate` | **FALTANDO** |
| 7114 | `InfluencePlayerScoreUpdate` | **FALTANDO** |

O cliente mantem os quatro valores na `InfluenceWindowProxy`: MMO, EIC, VRU e jogador. Nao existe command correspondente no servidor encontrado.

### 3.2 Domination relacionado

`_SafeCls_636`, **ID 23900**, herda o payload de `ID 4246` e acrescenta tres inteiros de estado de Domination. As notificacoes do cliente sao `INIT`, `UPDATE_VIEW` e `CLEANUP`.

| ID | Nome logico | Status |
|---:|---|---|
| 23900 | `DominationInfluenceUpdate` | **FALTANDO** |

Os tres inteiros adicionais devem ser mantidos com as rotacoes exatas do serializer decompilado; os nomes semanticos continuam **MAPEAR**.

## 4. Capture The Beacon (CTB) — protocolo legacy

O cliente possui parser dedicado em `_SafePkg_451\_SafeCls_450.as`, com os subcomandos `m`, `p`, `s`, `z`, `c` e `r`. O servidor possui apenas as constantes correspondentes em `Net\netty\ServerCommands.cs`; nao encontrei emissao nem manager de CTB.

Forma servidor -> cliente observada:

```text
0|n|ctb|<subcomando>|<parametros...>
```

| Subcomando | Payload | Uso no cliente | Status |
|---|---|---|---|
| `m` | `0` ou `1` | abre/inicializa ou fecha/limpa a janela CTB | **FALTANDO** |
| `p` | `key:int` + `value:int` | atualiza uma linha/celula do placar; os digitos `[1]` e `[2]` da key selecionam faccao/linha | **PARCIAL** |
| `s` | `mmo:Number` + `eic:Number` + `vru:Number` | atualiza os tres placares | **FALTANDO** |
| `z` | `assetId:int` + `faction:int` + `x:int` + `y:int` | cria uma home zone do beacon | **FALTANDO** |
| `c` | `assetId:int` + `userId:int` | anexa o beacon/marker a um usuario | **FALTANDO** |
| `r` | `assetId:int` | remove o beacon/marker anexado | **FALTANDO** |

O `p` e marcado como parcial porque `ServerCommands.cs` chama o subcomando de “beacon position”, mas o parser do cliente usa os dois inteiros para atualizar uma linha do scoreboard. O wire format esta confirmado; o significado do primeiro inteiro precisa de log de trafego ou de uma fonte server-side antiga.

O servidor deve implementar estado CTB antes de emitir esses comandos: partida aberta, placares, home zones, beacon carregado por usuario e remocao. O `BeaconCommand` moderno (`ID 10084`) nao substitui esse protocolo.

## 5. TDM e outros pacotes de evento

### 5.1 Team Deathmatch

O cliente possui uma familia de placar TDM ausente no servidor:

| ID | Tipo | Payload observado | Status |
|---:|---|---|---|
| 3826 | `TDMStatusUpdate` | dois inteiros de tempo/estado, vetor de `TDMFactionScore`, inteiro, vidas restantes e vetor geral de scores | **FALTANDO** |
| 24942 | `TDMFactionScore` | `score:int` + `faction:_SafeCls_583` | **FALTANDO** |
| 13039 | `TDMMatchResult` | placares, vencedor, ranking e `Vector<Reward>` | **FALTANDO** |
| 18631 | `Reward` | `lootId:String` + `amount:int` | **FALTANDO neste fluxo** |

`TDMStatusProxy` consome segundos restantes, vidas, placar da faccao do jogador, adversario e placar geral. As constantes legacy `tdm`, `drf`, `gms`, `msg`, `evt`, `dmz`, `go!`, `nfo` e `fnl` tambem aparecem no cliente, mas o servidor so possui constantes textuais sem fluxo completo.

### 5.2 Pacotes genericos de ativacao de evento

Outros candidatos encontrados no dump e nao encontrados por ID no servidor:

| ID | Payload | Uso observado | Status |
|---:|---|---|---|
| 23438 | `type:short`, `active:Boolean`, `Vector<EventAttribute>` | ativa/desativa evento no `EventProxy` | **FALTANDO** |
| 9493 | classe de atributo de evento | tipo aninhado do `23438` | **FALTANDO** |
| 20489 | `replacement:uint`, `mapId:int` | troca de mapa/evento de futebol | **FALTANDO** |
| 8793 | `faction:_SafeCls_583` | atributo de evento por faccao | **FALTANDO** |
| 3248 | `value:int` | atributo numerico de evento | **FALTANDO** |

O cliente tambem possui os eventos `football_4-4`, `football_invasion` e `football_tdm`, alem de notificacoes de `DOMINATION_FACTION`, troca de mapa e HQ demolida. Esses pacotes sao secundarios em relacao aos beacons, mas indicam que o servidor ainda nao implementa o `EventProxy` moderno.

## 6. O que ja existe e nao deve ser duplicado

### 6.1 Assets genericos

Os seguintes pacotes ja estao no servidor e devem ser usados como base:

| ID | Pacote | Estado |
|---:|---|---|
| 13718 | `AssetCreateCommand` | **IMPLEMENTADO** |
| 8437 | `AssetInfoCommand` | **IMPLEMENTADO** |
| 3397 | `AssetRemoveCommand` | **IMPLEMENTADO** |
| 30787 | `MapAssetActionAvailableCommand` | **IMPLEMENTADO**, mas hoje cobre portais/estacoes; nao liga a captura Sector Control |

O cliente mapeia `mapasset_sector_control_beacon` para o tipo numerico `42`. O servidor chama esse valor de `SECTOR_CONTROL_BATTLEMASTER` em `AssetTypeModule.cs`; o numero coincide, mas a nomenclatura precisa ser revisada para nao confundir battle master com beacon capturavel.

### 6.2 BeaconCommand existente

`BeaconCommand`, **ID 10084**, ja e emitido no login e em zonas especiais. Seu payload e estado do jogador: reparo, bot, demi-zone, radiation-zone e inteiros de status. Ele nao contem hash de beacon, progresso, faccao contestante ou carga de captura.

Status: **IMPLEMENTADO para estado do jogador; INCOMPATIVEL como Sector Control beacon**.

### 6.3 Relay stations do LoW

`GroupMapRelayStation` ja representa beacons com carga:

```text
assetType = RELAY_STATION (31)
ids = 100000101 .. 100000104
progress = 0 .. 100
create = AssetCreateCommand (13718)
remove = AssetRemoveCommand (3397)
ping = GroupPingCommand (20661)
```

Esse e o melhor exemplo existente de carga visivel e reutilizavel no servidor. A logica esta em `Game\Events\GroupMap200Manager.cs`. Ela pode servir de base estrutural para um `SectorControlBeacon`, mas os IDs 15267/24512 e os estados de faccao ainda precisam ser implementados.

### 6.4 Outros eventos verificados

Invasion Gate, Spaceball, Jackpot Battle, Scoremageddon, UBA, Duel/Training Grounds, LoW/Group Map 200 e Battle Station possuem partes implementadas no servidor. Battle Station tem captura por faccao e progresso de estacao, mas nao usa o contrato de Sector Control. Invasion ja possui documentacao separada em `INVASION_PACKETS.md`.

## 7. Ores, quests e action bar — verificacao do workspace

Esta secao registra pacotes que foram marcados como faltantes em inventarios antigos, mas que ja possuem arquivos no estado atual do workspace.

### 7.1 Ores modernos

| ID | Pacote | Estado atual |
|---:|---|---|
| 28690 | `CreateOreCommand` | **IMPLEMENTADO** |
| 24146 | `CollectOreRequest` + handler | **IMPLEMENTADO** |
| 20293 | `OreStackCommand` | **IMPLEMENTADO** |
| 11900 | `OreCountUpdateCommand` | **IMPLEMENTADO** |
| 30352 | `OreCargoUpdateCommand` | **IMPLEMENTADO / VALIDAR CONTEXTO** |
| 31038 | `OreRefinementEntryCommand` | **IMPLEMENTADO** |
| 12658 | `OreRefinementUpdateCommand` | **IMPLEMENTADO** |
| 14534 | `RefineOreRequest` | **IMPLEMENTADO** |
| 25203 | `SellOreRequest` | **IMPLEMENTADO** |
| 5888 | `TradeOreRequest` | **PARCIAL**: request/handler estao registrados, mas o handler ainda reutiliza `TrySellOre()` e nao implementa uma troca distinta. |

Os pontos restantes sao validacao de trafego, mapeamento dos nove tipos de recurso e confirmacao da remocao visual do ore coletado.

### 7.2 Quests modernas

Os requests `28872`, `13518`, `21421`, `23727`, `21988`, `5503` e `27259` possuem requests/handlers no workspace. Os commands `16203`, `26745` e `8537` agora possuem serializers e emissao: a lista inclui a quest fixa 1, detalhes enviam definicao/restricao e rating, e atualizacoes sao enviadas a cada sincronizacao. Ainda ha necessidade de validar os modelos aninhados em trafego real e expandir o catalogo de quests.

### 7.3 Action bar

Os IDs `18681`, `31697`, `30106`, `14317`, `2072`, `14222` e `31344` possuem implementacao. O `ID 18889` tambem foi completado para validar `SELECT`/`ACTIVATE`, `barType`, selecao de categorias e itens de uso unico. O restante da action bar usa os fluxos existentes de `SettingsManager`.

## 8. Ordem recomendada para implementar os faltantes

1. Criar `SectorControlManager` com partida, beacon, owner, contestantes, progressao `0..100`, lock e tickets.
2. Implementar `ID 15267` e `ID 24512`, reutilizando o estado de carga representado por `sectorHash`, faccao, direcao e percentagem.
3. Implementar `ID 19068`, `ID 12308` com novo contexto sem colisao e `ID 12560`/requests de lobby.
4. Implementar `ID 4246` e `ID 7114` para a janela de influencia; observar a ordem wire VRU/EIC/MMO do `ID 4246`.
5. Implementar CTB legacy (`m`, `p`, `s`, `z`, `c`, `r`) e seu estado de beacon anexado.
6. Implementar a familia TDM e o `EventProxy` generico.
7. Validar por log de trafego os campos marcados **MAPEAR**, principalmente timers, bonus e os inteiros de Domination/CTB.

Fontes do cliente mais importantes:

```text
G:\xpp\Tools\swf_work\main_current\decompiled_scripts\scripts\_SafePkg_142\_SafeCls_1055.as
G:\xpp\Tools\swf_work\main_current\decompiled_scripts\scripts\_SafePkg_142\_SafeCls_799.as
G:\xpp\Tools\swf_work\main_current\decompiled_scripts\scripts\_SafePkg_142\_SafeCls_996.as
G:\xpp\Tools\swf_work\main_current\decompiled_scripts\scripts\_SafePkg_142\_SafeCls_635.as
G:\xpp\Tools\swf_work\main_current\decompiled_scripts\scripts\_SafePkg_142\_SafeCls_725.as
G:\xpp\Tools\swf_work\main_current\decompiled_scripts\scripts\_SafePkg_451\_SafeCls_450.as
G:\xpp\Tools\swf_work\main_current\decompiled_scripts\scripts\_SafePkg_1939\SectorControlProxy.as
G:\xpp\Tools\swf_work\main_current\decompiled_scripts\scripts\_SafePkg_2125\InfluenceWindowProxy.as
```

## 9. Mensagens de texto, logs e notificacoes na tela

Esta e a parte mais importante para mensagens como “item coletado”, recompensa e avisos de evento. O cliente diferencia **log** (historico da interface) de **notificacao visual** (popup/aviso imediato).

### 9.1 Contratos textuais principais

| Formato esperado | Destino no cliente | Estado no servidor |
|---|---|---|
| `0|A|STD|<texto>` | Log literal | **IMPLEMENTADO** |
| `0|A|STM|<localizationKey>` | Log localizado | **IMPLEMENTADO** |
| `0|A|STM|<key>|<wildcard>|<value>|...` | Log localizado com substituicoes | **IMPLEMENTADO** |
| `0|LM|ST|<code>|<delta>|<total>` | Log de recompensa/saldo | **IMPLEMENTADO** para `URI`, `CRE`, `HON`, `EP` e `JPE` |
| `0|y|<code>|...` | Resposta de coleta/recompensa | **IMPLEMENTADO** para `CAR` |
| `0|n|MSG|<mode>|<style>|<key>|<rawReplacementList>` | Log ou notificacao conforme modo | **IMPLEMENTADO**; usado pelo Group Gate |
| `0|n|KSMSG|<localizationKey>` | Notificacao visual com som | **IMPLEMENTADO**; usado por Jackpot Battle/Training Grounds |
| `0|n|fbo|<boosterType>|<hours>` | Booster encontrado | **FALTANDO** como codigo `n`; ha equivalente por `A|STM` |
| `0|e|<oreId>` | Feedback de coleta de ore pelo heroi | **FALTANDO** |
| `0|A|KMS|...` | Text box antigo | **NAO CONFIRMADO**; constante existe, consumidor ativo nao foi localizado |
| `0|RCO|...` / `0|BCO|...` | Codigos declarados de coleta | **NAO CONFIRMADO** |

O cliente atual encaminha `A|STD` e `A|STM` pelo assembly de atributos; `LM` e `y` passam pelo assembly de coleta; `n|MSG` e `n|KSMSG` passam pelo assembly de eventos. Usar o envelope correto e necessario para a mensagem chegar ao componente visual desejado.

### 9.2 `A|STM`: mensagem localizada com wildcard

Formato esperado:

```text
0|A|STM|<localizationKey>
0|A|STM|<localizationKey>|<wildcard>|<value>|<wildcard>|<value>
```

Exemplos que ja existem no servidor:

```text
0|A|STM|peacearea
0|A|STM|msg_jackpot_players_left|%COUNT%|3
0|A|STM|jp_no_attack_n_seconds|%!|10
0|A|STM|booster_found|%BOOSTERNAME%|DARKORBIT_BOOSTER_HONOR|%HOURS%|2
```

Esse e o formato preferivel para mensagens textuais novas. Ele e usado, entre outros pontos, por `Game\Events\Duel.cs`, `Game\Objects\Attackable.cs`, `Game\Objects\Pet.cs` e `Game\Objects\Players\Managers\BoosterManager.cs`.

### 9.3 `MSG`: notificacao visual localizada

O cliente atual le:

```text
0|n|MSG|<mode:int>|<style:int>|<localizationKey>|<rawReplacementList opcional>
```

Substituicoes usam:

```text
{w:%WILDCARD%,v:<valor>},{w:%OUTRO%,v:<valor>}
```

Exemplo funcional em `Game\Events\GroupMap200Manager.cs`:

```text
0|n|MSG|5|0|msg_groupgate_waiting_for_group_members|{w:%MISSINGMEMBERCOUNT%,v:2},{w:%WAITINGTIME%,v:30}
```

Esse pacote deve ser escolhido quando a mensagem precisa aparecer na tela, e nao somente no historico de log. O `mode` determina no cliente se o resultado vai para log, notificacao ou ambos.

### 9.4 `MessageLocalizedWildcardCommand` binario

O cliente atual registra o comando top-level `ID 11751`. O serializer de `Net\netty\commands\MessageLocalizedWildcardCommand.cs` escreve:

```text
short 11751
short -27064
short -14597
int replacementCount
repeat replacementCount:
    short 1059
    UTF replacementValue
    short 24892
    short formatType
    short 26386
    short 28496
    UTF wildcard
UTF baseKey
short 24892
short formatType
short 26386
```

No modulo `ClientUITooltipTextFormatModule`, os valores observados sao `0 = PLAIN` e `5 = LOCALIZED`. O pacote esta **IMPLEMENTADO** e e usado por `Quests.CompleteQuest1()` e pelas opcoes da kill screen; ainda nao existe um emissor comum para todas as recompensas.

## 10. Item coletado e codigos de recompensa

O assembly de coleta do `main_current` conhece os seguintes codigos. A tabela separa o contrato que o cliente entende do emissor encontrado no servidor.

| Codigo | Payload esperado | Efeito do cliente | Estado do emissor atual |
|---|---|---|---|
| `CAR` | `resourceKey`, `amount` | Ore coletado, `farmresult`/`log_msg_gather_*` | **IMPLEMENTADO** em `Ore.Reward()` como `0|y|CAR|<ResourceKey>|<amount>` |
| `CRE` | `delta`, `total` | Creditos | **IMPLEMENTADO** via `0|LM|ST|CRE|...` |
| `URI` | `delta`, `total` | Uridium | **IMPLEMENTADO** via `0|LM|ST|URI|...` |
| `HON` | `delta`, `total` | Honra | **IMPLEMENTADO** via `0|LM|ST|HON|...` |
| `EP` | `delta`, `total`, `level` | Experiencia | **IMPLEMENTADO** via `0|LM|ST|EP|...` |
| `JPE` | `delta`, `total` | Jackpot | **IMPLEMENTADO** via `0|LM|ST|JPE|...` |
| `HTP` | `amount` | Hitpoints | **FALTANDO** emissor de loot confirmado |
| `ROK` | `rocketType`, `amount` | Foguetes | **FALTANDO** |
| `BAT` | `batteryType`, `amount` | Municao laser | **FALTANDO** |
| `FW` | `fireworkType`, `amount` | Fireworks | **FALTANDO** |
| `XEN` | `amount` | Extra energy | **PARCIAL**; `BonusBox.cs` usa `A|STD` literal |
| `JV` | `amount` | Jump vouchers | **PARCIAL** |
| `PFL` | `amount` | Combustivel PET | **FALTANDO** emissor de loot |
| `LOT` | `lootId`, `amount` | Item generico com `log_msg_gather_<category>_s/p` | **FALTANDO** emissor generico |
| `AMI` | `mineCode`, `amount` | Minas | **FALTANDO** emissor de loot |
| `DIS` | `type`, `amount` | Desconto | **FALTANDO** |
| `NB` | `type`, `amount` | Newbie booster | **FALTANDO** |
| `LOG` | dados de log | Log file | **FALTANDO** |
| `MIN` | dados da mina | Explosao/efeito da mina | **PARCIAL**; o servidor usa `0|n|MIN|<hash>` para o efeito |

Para reproduzir “item coletado” no cliente atual, usar:

```text
0|y|CAR|ore_prometium|1
0|y|CAR|ore_palladium|5
```

O cliente resolve `ore_prometium`/`ore_palladium` para a categoria e a chave localizada apropriada. Um `0|A|STD|Item collected...` funciona somente como texto literal e nao reproduz o fluxo nativo de loot.

### 10.1 Incompatibilidade em `Player.ChangeCargo()`

O servidor atualmente envia, ao alterar a carga:

```text
0|LM|STM|+<amount> <OreName>
```

Esse pacote mistura `LM` com o comando localizado `STM`. Para o assembly observado, os formatos seguros sao:

```text
0|y|CAR|<resourceKey>|<amount>
0|A|STM|<localizationKey>|<wildcard>|<value>
```

Portanto, o caminho atual de log de carga deve ser classificado como **INCOMPATIVEL/INSEGURO**, mesmo que a alteracao numerica de carga esteja correta.

`BonusBox.cs` tambem usa texto literal para extra energy e repair credits:

```text
0|A|STD| You received 10 galaxy gate extra energy
0|A|STD| You received 5 repair credits
```

Isso deve ser convertido para `XEN`, `JV`, `PFL`, `LOT` ou `A|STM` quando a meta for reproduzir o visual/localizacao do cliente.

## 11. Auditoria final de ore e quests no estado atual

Uma busca direta em `Net\netty\Handler.cs` confirmou que os requests abaixo estao registrados no dispatcher atual:

```text
CollectOreRequest.ID   -> CollectOreRequestHandler
SellOreRequest.ID      -> SellOreRequestHandler
TradeOreRequest.ID     -> TradeOreRequestHandler
RefineOreRequest.ID    -> RefineOreRequestHandler
QuestHandlerRegistration.AddTo(Commands)
```

Assim, esses IDs nao devem ser descritos como “nao registrados”. O que ainda falta e:

- `TradeOreRequest` tem semantica de venda, nao troca especifica;
- `XCP|<amount>` legacy nao foi encontrado no servidor;
- o mapeamento moderno cliente `0..8` para enum servidor `1..9` precisa ser mantido explicitamente;
- `OreTradeInfoCommand.cs` permanece sem fluxo completo;
- quest moderna ainda esta limitada a uma quest fixa: `QuestListUpdateCommand`, `QuestDetailsUpdateCommand` e `QuestUpdateCommand` ja sao enviados, mas sem catalogo geral;
- requests de quests estao conectados; filtros sao persistidos e a lista moderna e sincronizada, mas ainda nao representa um catalogo geral.

Estruturas modernas de ore ja implementadas e seus IDs:

```text
OreStackCommand             20293 (modulo aninhado)
OreResourceTypeModule        5452 (0..8)
OreCountUpdateCommand       11900
OreSyncContextModule        23579
OreCargoUpdateCommand       30352
RefinementTypeModule        30075
OreRefinementEntryCommand   31038 (modulo aninhado)
OreRefinementUpdateCommand  12658 (top-level)
```

O ID top-level da atualizacao de refinaria e `12658`; `31038` e somente a entrada aninhada.

## 12. Proximos passos para os pacotes realmente faltantes

1. Corrigir o emissor de recompensas de carga para `CAR`/`A|STM`.
2. Criar um emissor comum para `LOT`, `ROK`, `BAT`, `XEN`, `JV`, `PFL`, `AMI`, `DIS` e `NB`.
3. Implementar `XCP` e separar troca de palladium da venda comum.
4. Completar quest moderna com catalogo real, condicoes, ratings e recompensas; o envelope `26745` ja esta implementado.
5. Depois tratar starmap, inventario/hangar geral, estados auxiliares de PET e os eventos listados nas secoes anteriores.
