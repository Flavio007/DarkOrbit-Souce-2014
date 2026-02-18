# Invasion Gate Packets (Cliente 2014)

Este arquivo documenta os pacotes legados de Invasao usados pelo canal `MAP_EVENT` (`0|n|...`).

## Comandos

1. `isi` (`INIT_INVASION_SCOREBOARD`)
- Formato: `0|n|isi|<mmoScore>|<eicScore>|<vruScore>|<wave>`
- Campos:
  - `<mmoScore>`: pontuacao da MMO
  - `<eicScore>`: pontuacao da EIC
  - `<vruScore>`: pontuacao da VRU
  - `<wave>`: onda atual
- Mapeamento cliente AS:
  - `assembleInitInvasionScoreboard(param1[3], param1[4], param1[5], param1[6])`

2. `isc` (`SET_INVASION_SCORE`)
- Formato: `0|n|isc|<factionId>|<score>`
- Campos:
  - `<factionId>`: 1 = MMO, 2 = EIC, 3 = VRU
  - `<score>`: nova pontuacao da faccao
- Mapeamento cliente AS:
  - `assembleSetInvasionScore(param1[3], param1[4])`

3. `isw` (`SET_INVASION_WAVE`)
- Formato: `0|n|isw|<wave>`
- Campos:
  - `<wave>`: onda atual
- Mapeamento cliente AS:
  - `assembleSetInvasionWave(param1[3])`

## Exemplos

1. Inicializar janela de invasao:
```text
0|n|isi|0|0|0|1
```

2. Atualizar score da EIC para 125:
```text
0|n|isc|2|125
```

3. Atualizar onda para 7:
```text
0|n|isw|7
```

## Observacoes de compatibilidade

1. O cliente 2014 espera `isc` com **2 parametros** apos o comando (`factionId`, `score`).
2. O cliente 2014 espera `isi` com **4 parametros** apos o comando (`mmo`, `eic`, `vru`, `wave`).
3. Enviar contagem de parametros diferente pode quebrar atualizacao da UI de Invasao.
