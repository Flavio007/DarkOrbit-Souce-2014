# Regras do rastreador de missões

As definições ficam em `config/quests.xml`. IDs devem permanecer estáveis para preservar o progresso salvo. `minLevel` controla elegibilidade e ordenação; `factionId` é 0 para todas as companhias, 1 MMO, 2 EIC, 3 VRU.

## Restrições da missão

- `timeLimitSeconds`: prazo em segundos de relógio, incluindo desconexões; começa no aceite e é salvo no progresso.
- `maxDeaths`, `maxKills`, `maxDamageTaken`: limites inclusivos; -1 desativa. Exceder cancela a tentativa e permite um novo aceite.
- `forbidCloak="1"`: cancela ao detectar camuflagem.
- `forbiddenWeapon`: LASER, ROCKET ou ROCKET_LAUNCHER. O consumo de munição registra o disparo mesmo quando ele erra.

## Condições

`type`: killNpc, killNpcs, killPlayers, collectOre, collectCargoOre, collectCargoBox, collectBonusBox, sellOre, damage, travel, jump, visitMap, visitPosition, stayOnMap, radiation, visitMissionControl e joinClan.

`target` é a quantidade; para stayOnMap e radiation são segundos online observados, para travel são unidades de distância do mapa. visitPosition usa x, y e radius em coordenadas internas do servidor.

Filtros opcionais: targetShipId, targetOre, targetFactionId, targetMapId, mapRelation (home ou enemy) e weapon (nome do DamageType). Os filtros de mapa usam o mapa visual. Territórios neutros não são classificados como inimigos.

`sequence` maior que zero exige a conclusão das etapas numeradas anteriores. Condições com o mesmo `alternativeGroup` são alternativas; cada grupo exige pelo menos uma alternativa concluída. Condições sem grupo são obrigatórias.

`continuous="1"` reinicia o progresso ao deixar o mapa ou desconectar. `maxAbsenceSeconds` permite limitar a ausência antes de cancelar a tentativa. O prazo total não para durante a desconexão.

Recompensas: credits, uridium, honor, experience, jackpot e os loot IDs de munição aceitos por Player.AddAmmo.

## Validação

Compilação isolada com Roslyn 4.8 e referências do projeto, sem substituir o executável em uso. Os scripts de regressão existentes cobrem catálogo, facção, alvos, ícones e missão inicial. Fluxos completos com o cliente ainda exigem validação no jogo.

## Catálogo clássico da revisão 63014

`config/reference/mission-list-63014.json` é a lista permitida extraída do HTML do usuário: 174 entradas normais (inclui uma repetição de Hunt the hunter) e 10 diárias. Não é importada a lista nova online.

`classic-quest-specs.json` guarda fatos de objetivos e recompensas e suas fontes. `python config/reference/sync_classic_quests.py` sincroniza essas definições e atualiza os IDs de cobertura no inventário. IDs existentes não são reaproveitados para missões diferentes. `classic-quest-pending.json` identifica as contagens que não puderam ser confirmadas. As variantes de facção são definições distintas, filtradas no servidor; a companhia alvo nunca recebe sua própria missão de ataque ou roubo.

As chaves de tradução vêm do resource_quest.xml do cliente. Links incorretos de variantes na captura não substituem o nome/nível capturado. As duas reconstruções por regra ou conversão de unidades estão explicitadas no campo derivation das especificações. Diárias não têm nível indicado na captura e usam nível mínimo 1; após conclusão tornam-se disponíveis no próximo dia UTC, preservando tentativas ativas durante a virada do dia.

Filtros adicionais: targetShipIds (IDs separados por vírgula), targetNpcName (nome sem depender da pontuação decorativa), shipFamily (famílias de naves incluindo designs), targetMaps (nomes de mapas separados por vírgula), mapOwner, mapSector, enemyTarget, requireCloak (1 camuflado, -1 sem camuflagem), maxHpPercent e inRadiation. branch permite alternativas compostas: todas as condições obrigatórias e uma ramificação inteira devem estar completas. Dentro de uma ramificação, alternativeGroup e sequence continuam valendo.

stealCargoBox usa a companhia e nave do proprietário da carga, não do alienígena que a soltou. Uma caixa conta apenas uma vez, mesmo quando recolhida em partes. Objetivos de permanência contínua já terminados não são apagados ao viajar para cumprir o próximo mapa.

As recompensas especiais de diárias incluem extraEnergy, repairCredits, jumpCredits, bootyKeys e logdisks. Chaves e créditos ficam no estado persistente do jogador; logdisks usa o inventário skillTree já existente. O catálogo não adiciona novos modelos de NPC, mapas ou mecânicas de viagem à galáxia.
