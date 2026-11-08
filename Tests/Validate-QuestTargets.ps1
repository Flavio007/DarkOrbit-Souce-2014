param([string]$AssemblyPath='H:\Darkorbit-Cpp\build\server-quest-framing\DarkOrbit.exe')
$ErrorActionPreference='Stop'
[Environment]::CurrentDirectory=(Split-Path $PSScriptRoot -Parent)
. (Join-Path $PSScriptRoot 'Validate-TutorialRange.ps1') -AssemblyPath $AssemblyPath
$player.Data=[Activator]::CreateInstance($player.GetType().GetProperty('Data').PropertyType)
$player.Settings=[Activator]::CreateInstance($player.GetType().GetField('Settings').FieldType)
$player.Settings.ClassY2T.questsUnavailableFilter=$true;$player.Settings.ClassY2T.questsAvailableFilter=$true;$player.Settings.ClassY2T.questsCompletedFilter=$true
$questsType=$asm.GetType('Ow.Game.Objects.Players.Quests');$quests=[Activator]::CreateInstance($questsType,@($player))
$catalog=$asm.GetType('Ow.Game.Objects.Players.QuestCatalog');$all=$catalog.GetMethod('All').Invoke($null,@())
$include=$questsType.GetMethod('Include',[Reflection.BindingFlags]'Instance,NonPublic')
$stateType=$asm.GetType('Ow.Game.Objects.Players.QuestPlayerState')
foreach ($faction in 1..3) {
    $player.FactionId=$faction
    foreach ($quest in $all) {
        $state=[Activator]::CreateInstance($stateType,@([int]$quest.Id))
        foreach ($status in 0..2) {
            $state.State=$status;$visible=$include.Invoke($quests,@($quest,$state))
            if ($quest.FactionId -ne 0 -and $quest.FactionId -ne $faction -and $visible) { throw 'Foreign-faction quest visible in a filter' }
        }
        if ($quest.Title -match '^(MMO|EIC|VRU) - ') { throw 'Faction prefix retained in title' }
    }
    $patrol=$catalog.GetMethod('Get').Invoke($null,@([int]($faction*100+1))).Conditions[0]
    if (!$patrol.MatchesEvent('killNpc',84,0) -or $patrol.MatchesEvent('killNpc',71,0) -or $patrol.MatchesEvent('killNpc',0,0)) { throw 'Streuner target filter failed' }
    $ore=$catalog.GetMethod('Get').Invoke($null,@([int]($faction*100+3))).Conditions[0]
    if (!$ore.MatchesEvent('collectOre',0,1) -or $ore.MatchesEvent('collectOre',0,2) -or $ore.MatchesEvent('collectBonusBox',0,0)) { throw 'Prometium target filter failed' }
    $service=$catalog.GetMethod('Get').Invoke($null,@([int]($faction*100+4))).Conditions[0]
    if (!$service.MatchesEvent('killNpc',71,0) -or $service.MatchesEvent('killNpc',84,0)) { throw 'Lordakia target filter failed' }
    if ($patrol.IconKey -ne 'questTarget/npc_84_icon.png' -or $service.IconKey -ne 'questTarget/npc_71_icon.png' -or $ore.IconKey -ne 'trade/ore_1') { throw 'Missing target icons' }
}
$player.FactionId=1
$wire=$questsType.GetMethod('BuildModernDefinition',[Reflection.BindingFlags]'Instance,NonPublic').Invoke($quests,@($catalog.GetMethod('Get').Invoke($null,@([int]101)))).ToWireBytes()
if ([Text.Encoding]::UTF8.GetString($wire) -notmatch 'icon:questTarget/npc_84_icon.png') { throw 'Target icon missing from quest details wire' }
Write-Output 'Quest targets passed: faction filters in all states, clean titles, specific NPC/ore progress and icon metadata on wire'
